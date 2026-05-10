#-------------------------------------------------------------------------------
#   Generate an intermediate representation of a clang AST dump.
#-------------------------------------------------------------------------------
import re, json, sys, subprocess , os

def is_api_decl(decl, prefix, extra_api_prefixes=None):
    if 'name' in decl:
        if decl['name'].startswith(prefix):
            return True
        if extra_api_prefixes:
            for ep in extra_api_prefixes:
                if decl['name'].startswith(ep):
                    return True
        return False
    elif decl['kind'] == 'EnumDecl':
        # an anonymous enum, check if the items start with the prefix
        first = get_first_non_comment(decl['inner'])
        return first['name'].lower().startswith(prefix)
    else:
        return False

def get_first_non_comment(items):
    return next(i for i in items if i['kind'] != 'FullComment')

def strip_comments(items):
    return [i for i in items if i['kind'] != 'FullComment']

def extract_comment(comment, source):
    return source[comment['range']['begin']['offset']:comment['range']['end']['offset']+1].rstrip()

def is_dep_decl(decl, dep_prefixes):
    for prefix in dep_prefixes:
        if is_api_decl(decl, prefix):
            return True
    return False

def dep_prefix(decl, dep_prefixes):
    for prefix in dep_prefixes:
        if is_api_decl(decl, prefix):
            return prefix
    return None

def filter_types(str):
    return str.replace('_Bool', 'bool')

# TBD ELI
# def parse_struct(decl, source):
#     outp = {}
#     outp['kind'] = 'struct'
#     outp['name'] = decl['name']
#     outp['fields'] = []
#     for item_decl in decl['inner']:
#         if item_decl['kind'] == 'FullComment':
#             outp['comment'] = extract_comment(item_decl, source)
#             continue
#         if item_decl['kind'] != 'FieldDecl':
#             sys.exit(f"ERROR: Structs must only contain simple fields ({decl['name']})")
#         item = {}
#         if 'name' in item_decl:
#             item['name'] = item_decl['name']
#         item['type'] = filter_types(item_decl['type']['qualType'])
#         outp['fields'].append(item)
#     return outp
def parse_struct(decl, source):
    outp = {}
    outp['kind'] = 'struct'
    outp['name'] = decl.get('name', '')
    outp['fields'] = []
    if 'inner' in decl:
        for item_decl in decl['inner']:
            if item_decl['kind'] == 'FullComment':
                outp['comment'] = extract_comment(item_decl, source)
                continue
            if item_decl['kind'] != 'FieldDecl':
                print(f"Warning: Ignoring unsupported inner node of type {item_decl['kind']} in struct {decl.get('name','')}")
                continue  # Skip processing unsupported nodes
            item = {}
            if 'name' in item_decl:
                item['name'] = item_decl['name']
            item['type'] = filter_types(item_decl['type']['qualType'])
            outp['fields'].append(item)
    else:
        # No inner declarations - forward/opaque declaration, skip
        return None
    return outp

def parse_enum(decl, source):
    outp = {}
    if 'name' in decl:
        outp['kind'] = 'enum'
        outp['name'] = decl['name']
        needs_value = False
    else:
        outp['kind'] = 'consts'
        needs_value = True
    outp['items'] = []
    implicit_value = 0
    for item_decl in decl['inner']:
        if item_decl['kind'] == 'FullComment':
            outp['comment'] = extract_comment(item_decl, source)
            continue
        if item_decl['kind'] == 'EnumConstantDecl':
            item = {}
            item['name'] = item_decl['name']
            if 'inner' in item_decl:
                exprs = strip_comments(item_decl['inner'])
                if len(exprs) > 0:
                    const_expr = exprs[0]
                    if const_expr['kind'] != 'ConstantExpr':
                        sys.exit(f"ERROR: Enum values must be a ConstantExpr ({item_decl['name']}), is '{const_expr['kind']}'")
                    if const_expr['valueCategory'] != 'rvalue' and const_expr['valueCategory'] != 'prvalue':
                        sys.exit(f"ERROR: Enum value ConstantExpr must be 'rvalue' or 'prvalue' ({item_decl['name']}), is '{const_expr['valueCategory']}'")
                    const_expr_inner = strip_comments(const_expr['inner'])
                    if (len(const_expr_inner) == 1) and (const_expr_inner[0]['kind'] == 'IntegerLiteral'):
                        item['value'] = const_expr_inner[0]['value']
                    elif (len(const_expr_inner) == 1) and (const_expr_inner[0]['kind'] == 'UnaryOperator') and (const_expr_inner[0].get('opcode') == '-'):
                        unary_inner = strip_comments(const_expr_inner[0]['inner'])
                        if (len(unary_inner) == 1) and (unary_inner[0]['kind'] == 'IntegerLiteral'):
                            item['value'] = str(-int(unary_inner[0]['value']))
                        else:
                            sys.exit(f"ERROR: Enum value ConstantExpr must have exactly one IntegerLiteral ({item_decl['name']})")
                    elif 'value' in const_expr:
                        # Clang folds complex expressions (bit shifts, etc.) into ConstantExpr.value
                        item['value'] = const_expr['value']
                    else:
                        sys.exit(f"ERROR: Enum value ConstantExpr must have exactly one IntegerLiteral ({item_decl['name']})")
            if needs_value and 'value' not in item:
                item['value'] = str(implicit_value)
            implicit_value = int(item.get('value', implicit_value)) + 1
            outp['items'].append(item)
    return outp

def parse_func(decl, source):
    outp = {}
    outp['kind'] = 'func'
    outp['name'] = decl['name']
    outp['type'] = filter_types(decl['type']['qualType'])
    outp['params'] = []
    if 'inner' in decl:
        for param in decl['inner']:
            if param['kind'] == 'FullComment':
                outp['comment'] = extract_comment(param, source)
                continue
            # Skip attribute nodes (VisibilityAttr, UnusedAttr, etc.) that
            # appear when API macros expand to __attribute__((...)).
            if param['kind'].endswith('Attr'):
                continue
            if param['kind'] != 'ParmVarDecl':
                print(f"  >> warning: ignoring func {decl['name']} (unsupported parameter type)")
                return None
            if 'name' not in param:
                print(f"  >> warning: ignoring func {decl['name']} (unnamed parameter)")
                return None
            outp_param = {}
            outp_param['name'] = param['name']
            outp_param['type'] = filter_types(param['type']['qualType'])
            outp['params'].append(outp_param)
    return outp

def parse_decl(decl, source):
    kind = decl['kind']
    if kind == 'RecordDecl':
        return parse_struct(decl, source)
    elif kind == 'EnumDecl':
        return parse_enum(decl, source)
    elif kind == 'FunctionDecl':
        return parse_func(decl, source)
    elif kind == 'TypedefDecl':
        return parse_typedef_alias(decl)
    else:
        return None

def parse_typedef_alias(decl):
    """Parse a TypedefDecl as a struct/enum alias entry for the IR."""
    if 'type' not in decl or 'qualType' not in decl['type']:
        return None
    return {
        'kind': 'typedef_alias',
        'name': decl['name'],
        'type': decl['type']['qualType'],
    }

# def clang(csrc_path, with_comments=False):
#     cmd = ['clang', '-Xclang', '-ast-dump=json', "-c", csrc_path]
#     if with_comments:
#         cmd.append('-fparse-all-comments')
#     return subprocess.check_output(cmd)

def clang(csrc_path, with_comments=False, extra_include_dirs=None):
    import os
    ext = os.path.splitext(csrc_path)[1]
    if ext == '.cpp':
        compiler = "clang++"
        std_flag = "-std=c++17"
    else:
        compiler = "clang"
        std_flag = ""
    cmd = [compiler]
    if std_flag:
        cmd.append(std_flag)
    # Add include path for parent directory (needed to find ../ext/sokol/*.h)
    cmd.append('-I..')
    # Add any extra include dirs required by specific libraries
    if extra_include_dirs:
        for inc in extra_include_dirs:
            cmd.append(f'-I{inc}')
    cmd += ['-Xclang', '-ast-dump=json', "-c", csrc_path]
    if with_comments:
        cmd.append('-fparse-all-comments')
    return subprocess.check_output(cmd)


def gen(header_path, source_path, module, main_prefix, dep_prefixes, with_comments=False, extra_include_dirs=None, extra_api_prefixes=None):
    ast = clang(source_path, with_comments=with_comments, extra_include_dirs=extra_include_dirs)
    inp = json.loads(ast)
    outp = {}
    outp['module'] = module
    outp['prefix'] = main_prefix
    outp['dep_prefixes'] = dep_prefixes
    outp['decls'] = []
    # load string with original line endings (otherwise Clang's output ranges
    # for comments are off)
    # NOTE: that same problem might exist for non-ASCII characters,
    # so don't use those in header files!
    with open(header_path, mode='r', newline='') as f:
        source = f.read()
    with open(header_path, mode='r', newline='') as f:
        source = f.read()
        match = re.search(r"/\*(.*?)\*/", source, re.S)
        if match:
            first_comment = match.group(1)
            if first_comment and "Project URL" in first_comment:
                outp['comment'] = first_comment
        # Pre-process to find anonymous RecordDecls that should be associated with TypedefDecls
        decls_to_process = []
        i = 0
        while i < len(inp['inner']):
            decl = inp['inner'][i]
            
            # Check if this is an anonymous RecordDecl followed by a TypedefDecl
            if (decl.get('kind') == 'RecordDecl' and 
                not decl.get('name') and  # anonymous
                'inner' in decl and len(decl['inner']) > 0 and  # has fields
                i + 1 < len(inp['inner'])):
                
                next_decl = inp['inner'][i + 1]
                if (next_decl.get('kind') == 'TypedefDecl' and 
                    next_decl.get('name') and
                    is_api_decl(next_decl, main_prefix, extra_api_prefixes)):
                    
                    # This is a typedef struct pattern, merge them
                    merged_decl = decl.copy()
                    merged_decl['name'] = next_decl['name']  # Use the typedef name
                    decls_to_process.append(merged_decl)
                    i += 2  # Skip both the RecordDecl and TypedefDecl
                    continue

            # Check if this is an anonymous EnumDecl followed by a TypedefDecl
            # e.g. typedef enum { ... } ma_format;
            if (decl.get('kind') == 'EnumDecl' and
                not decl.get('name') and  # anonymous
                'inner' in decl and len(decl['inner']) > 0 and
                i + 1 < len(inp['inner'])):

                next_decl = inp['inner'][i + 1]
                # Also verify the typedef actually refers to this enum (not a struct/other type).
                # In the clang AST, a typedef over an anonymous enum has an inner ElaboratedType
                # whose qualType starts with 'enum '; a typedef over a struct starts with 'struct '.
                next_refers_to_enum = (
                    next_decl.get('kind') == 'TypedefDecl' and
                    any(inner.get('kind') == 'ElaboratedType' and
                        inner.get('type', {}).get('qualType', '').startswith('enum ')
                        for inner in next_decl.get('inner', []))
                )
                if (next_refers_to_enum and
                    next_decl.get('name') and
                    is_api_decl(next_decl, main_prefix, extra_api_prefixes)):

                    # This is a typedef enum pattern, merge them
                    merged_decl = decl.copy()
                    merged_decl['name'] = next_decl['name']  # Use the typedef name
                    decls_to_process.append(merged_decl)
                    i += 2  # Skip both the EnumDecl and TypedefDecl
                    continue
            
            decls_to_process.append(decl)
            i += 1
        
        # Now process the merged declarations
        for decl in decls_to_process:
            is_dep = is_dep_decl(decl, dep_prefixes)
            if is_api_decl(decl, main_prefix, extra_api_prefixes) or is_dep:
                outp_decl = parse_decl(decl, source)
                if outp_decl is not None:
                    outp_decl['is_dep'] = is_dep
                    outp_decl['dep_prefix'] = dep_prefix(decl, dep_prefixes)
                    outp['decls'].append(outp_decl)
    with open(f'{module}.json', 'w') as f:
        f.write(json.dumps(outp, indent=2));
    return outp
