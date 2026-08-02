package com.sokol.billing;

import android.app.Activity;

import com.android.billingclient.api.AcknowledgePurchaseParams;
import com.android.billingclient.api.BillingClient;
import com.android.billingclient.api.BillingClientStateListener;
import com.android.billingclient.api.BillingFlowParams;
import com.android.billingclient.api.BillingResult;
import com.android.billingclient.api.PendingPurchasesParams;
import com.android.billingclient.api.ProductDetails;
import com.android.billingclient.api.Purchase;
import com.android.billingclient.api.QueryProductDetailsParams;
import com.android.billingclient.api.QueryPurchasesParams;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Play Billing helper for the sokol_billing plugin. Called from native code
 * (sokol_billing_android.c); results flow back through nativeOnEvent into the
 * plugin's C event queue, drained by the app's game thread.
 *
 * One-time (INAPP) products only. Purchases are acknowledged here as soon as
 * they are reported PURCHASED (Play refunds unacknowledged purchases after
 * three days); the app persists the proof from the same event.
 */
public final class SokolBilling {

    /** Upcall into sokol_billing_android.c — safe from any thread. */
    static native void nativeOnEvent(int type, int code, String sku, String price,
                                     String proof, String signature);

    /* Mirror of sokolbilling_event_type in sokol_billing.h. */
    static final int EV_PRODUCT_INFO       = 1;
    static final int EV_PRODUCT_FAILED     = 2;
    static final int EV_PURCHASE_OK        = 3;
    static final int EV_PURCHASE_CANCELLED = 4;
    static final int EV_PURCHASE_FAILED    = 5;
    static final int EV_RESTORE_DONE       = 6;
    static final int EV_SYNC_DONE          = 7;

    static Activity activity;
    static BillingClient client;
    static boolean ready;
    static final List<Pending> pending = new ArrayList<>();
    static final Map<String, ProductDetails> products = new HashMap<>();

    private SokolBilling() {}

    public static void init(final Activity act) {
        activity = act;
        act.runOnUiThread(() -> {
            if (client != null) return;
            client = BillingClient.newBuilder(act)
                .enablePendingPurchases(
                    PendingPurchasesParams.newBuilder().enableOneTimeProducts().build())
                .setListener(SokolBilling::onPurchasesUpdated)
                .build();
            connect();
        });
    }

    static void connect() {
        client.startConnection(new BillingClientStateListener() {
            @Override public void onBillingSetupFinished(BillingResult r) {
                if (r.getResponseCode() != BillingClient.BillingResponseCode.OK) {
                    // ⛔ FAIL the queued work; do not just return. Leaving it in `pending`
                    // strands it forever: offline, a queued restore never reaches
                    // queryPurchasesAsync, so RESTORE_DONE is never emitted and the caller
                    // waits on "contacting the store…" with no way out (user-reported
                    // 2026-08-01, airplane mode). A caller must always get an answer.
                    failPending(r.getResponseCode());
                    return;
                }
                List<Pending> run;
                synchronized (pending) {
                    ready = true;
                    run = new ArrayList<>(pending);
                    pending.clear();
                }
                for (Pending x : run) x.onReady.run();
                /* Replay owned purchases so the app's entitlement cache heals
                   (refunds/family changes reconcile the same way on resume). */
                queryOwned(false);
            }
            @Override public void onBillingServiceDisconnected() {
                synchronized (pending) { ready = false; }
                // Anything queued at this moment would wait for a reconnect that only a
                // future call triggers. An explicit failure beats an indefinite hang.
                failPending(BillingClient.BillingResponseCode.SERVICE_DISCONNECTED);
            }
        });
    }

    static void failPending(int code) {
        List<Pending> fail;
        synchronized (pending) { fail = new ArrayList<>(pending); pending.clear(); }
        for (Pending p : fail) p.onFail.run(code);
    }

    /** A queued request: what to run once connected, and how to answer if we never connect. */
    interface FailCallback { void run(int code); }
    static final class Pending {
        final Runnable onReady; final FailCallback onFail;
        Pending(Runnable onReady, FailCallback onFail) { this.onReady = onReady; this.onFail = onFail; }
    }

    /** Run r once the client is connected; reconnect lazily if the service dropped.
        onFail is invoked instead if the connection cannot be established. */
    static void whenReady(Runnable r, FailCallback onFail) {
        boolean now;
        synchronized (pending) {
            now = ready && client != null && client.isReady();
            if (!now) pending.add(new Pending(r, onFail));
        }
        if (now) {
            r.run();
        } else if (client != null && activity != null) {
            activity.runOnUiThread(() -> { if (!client.isReady()) connect(); });
        } else {
            // No client/activity at all — nothing will ever drain the queue.
            failPending(BillingClient.BillingResponseCode.SERVICE_UNAVAILABLE);
        }
    }

    public static void queryProduct(final String sku) {
        whenReady(() -> queryDetails(sku, details -> {
            if (details != null) {
                ProductDetails.OneTimePurchaseOfferDetails offer =
                    details.getOneTimePurchaseOfferDetails();
                nativeOnEvent(EV_PRODUCT_INFO, 0, sku,
                    offer != null ? offer.getFormattedPrice() : "", null, null);
            } else {
                nativeOnEvent(EV_PRODUCT_FAILED, 0, sku, null, null, null);
            }
        }), code -> nativeOnEvent(EV_PRODUCT_FAILED, code, sku, null, null, null));
    }

    public static void purchase(final String sku) {
        whenReady(() -> queryDetails(sku, details -> {
            if (details == null) {
                nativeOnEvent(EV_PURCHASE_FAILED,
                    BillingClient.BillingResponseCode.ITEM_UNAVAILABLE, sku, null, null, null);
                return;
            }
            activity.runOnUiThread(() -> {
                BillingFlowParams params = BillingFlowParams.newBuilder()
                    .setProductDetailsParamsList(Collections.singletonList(
                        BillingFlowParams.ProductDetailsParams.newBuilder()
                            .setProductDetails(details)
                            .build()))
                    .build();
                BillingResult r = client.launchBillingFlow(activity, params);
                if (r.getResponseCode() != BillingClient.BillingResponseCode.OK) {
                    nativeOnEvent(EV_PURCHASE_FAILED, r.getResponseCode(), sku, null, null, null);
                }
                /* OK -> the result arrives in onPurchasesUpdated. */
            });
        }), code -> nativeOnEvent(EV_PURCHASE_FAILED, code, sku, null, null, null));
    }

    public static void restore() {
        whenReady(() -> queryOwned(true),
                  code -> nativeOnEvent(EV_RESTORE_DONE, code, null, null, null, null));
    }

    /** Background re-enumeration (app foregrounded). Same query as restore(), but it completes
        with SYNC_DONE so a consumer never mistakes it for the answer to a user's Restore tap. */
    public static void sync() {
        whenReady(() -> queryOwned(false),
                  code -> nativeOnEvent(EV_SYNC_DONE, code, null, null, null, null));
    }

    interface DetailsCallback { void run(ProductDetails details); }

    static void queryDetails(final String sku, final DetailsCallback cb) {
        ProductDetails cached;
        synchronized (products) { cached = products.get(sku); }
        if (cached != null) { cb.run(cached); return; }

        QueryProductDetailsParams params = QueryProductDetailsParams.newBuilder()
            .setProductList(Collections.singletonList(
                QueryProductDetailsParams.Product.newBuilder()
                    .setProductId(sku)
                    .setProductType(BillingClient.ProductType.INAPP)
                    .build()))
            .build();
        client.queryProductDetailsAsync(params, (r, list) -> {
            ProductDetails found = null;
            if (r.getResponseCode() == BillingClient.BillingResponseCode.OK && list != null) {
                for (ProductDetails d : list) {
                    if (sku.equals(d.getProductId())) { found = d; break; }
                }
            }
            if (found != null) {
                synchronized (products) { products.put(sku, found); }
            }
            cb.run(found);
        });
    }

    /** Emit PURCHASE_OK for every owned purchase, then the matching completion event:
        RESTORE_DONE for a user-initiated restore, SYNC_DONE for the automatic replay. */
    static void queryOwned(final boolean explicitRestore) {
        QueryPurchasesParams params = QueryPurchasesParams.newBuilder()
            .setProductType(BillingClient.ProductType.INAPP)
            .build();
        client.queryPurchasesAsync(params, (r, purchases) -> {
            int rc = r.getResponseCode();
            if (rc == BillingClient.BillingResponseCode.OK) {
                for (Purchase p : purchases) reportPurchase(p);
            }
            // ⛔ Carry the response code. A FAILED query reports zero purchases, which is
            // indistinguishable from "you own nothing" unless the consumer is told the query
            // never answered — and an entitlement cache that reconciles against that will
            // revoke a paying customer for being offline. code 0 (OK) = the store answered.
            //
            // The AUTOMATIC replay must also be announced (as SYNC_DONE): the PURCHASE_OK
            // stream is add-only, so without a completion event nothing downstream can ever
            // learn that a purchase was refunded/revoked — Google's `queryPurchasesAsync`
            // guidance is that the returned set IS the entitlement. A separate type keeps it
            // from being mistaken for the answer to a user's "Restore purchases" tap.
            nativeOnEvent(explicitRestore ? EV_RESTORE_DONE : EV_SYNC_DONE,
                          rc, null, null, null, null);
        });
    }

    static void onPurchasesUpdated(BillingResult r, List<Purchase> purchases) {
        int code = r.getResponseCode();
        if (code == BillingClient.BillingResponseCode.OK && purchases != null) {
            for (Purchase p : purchases) reportPurchase(p);
        } else if (code == BillingClient.BillingResponseCode.USER_CANCELED) {
            nativeOnEvent(EV_PURCHASE_CANCELLED, code, null, null, null, null);
        } else if (code == BillingClient.BillingResponseCode.ITEM_ALREADY_OWNED) {
            /* Owned but the app lost the cache — replay it. */
            queryOwned(false);
        } else {
            nativeOnEvent(EV_PURCHASE_FAILED, code, null, null, null, null);
        }
    }

    static void reportPurchase(Purchase p) {
        if (p.getPurchaseState() != Purchase.PurchaseState.PURCHASED) return;
        List<String> skus = p.getProducts();
        String sku = skus.isEmpty() ? "" : skus.get(0);
        nativeOnEvent(EV_PURCHASE_OK, 0, sku, null, p.getOriginalJson(), p.getSignature());
        if (!p.isAcknowledged()) {
            client.acknowledgePurchase(
                AcknowledgePurchaseParams.newBuilder()
                    .setPurchaseToken(p.getPurchaseToken())
                    .build(),
                result -> {});
        }
    }
}
