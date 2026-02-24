import AppIntents
import WidgetKit
import Foundation

/// AppIntent that calls the FEFO quick-consume API directly from the Lock Screen widget.
/// Consumes 1 unit of the next expiring product without opening the app.
@available(iOS 17.0, *)
struct ConsumeNextExpiringIntent: AppIntent {
    static var title: LocalizedStringResource = "Consume Next Expiring"
    static var description = IntentDescription("Consume one unit of the next expiring product")

    func perform() async throws -> some IntentResult {
        let appGroupId = "group.com.famick.homemanagement"

        // 1. Read access token and base URL from shared keychain
        guard let accessToken = KeychainHelper.getAccessToken(),
              let baseUrl = KeychainHelper.getBaseUrl() else {
            // Not logged in or tokens unavailable - silently fail
            return .result()
        }

        // 2. Read next expiring product from App Group UserDefaults
        guard let defaults = UserDefaults(suiteName: appGroupId),
              let jsonString = defaults.string(forKey: "WidgetExpiringProducts"),
              let jsonData = jsonString.data(using: .utf8),
              let products = try? JSONDecoder().decode([WidgetProductItem].self, from: jsonData),
              let product = products.first else {
            return .result()
        }

        // 3. POST quick-consume API call
        guard let url = URL(string: "\(baseUrl)/api/v1/stock/quick-consume") else {
            return .result()
        }

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("Bearer \(accessToken)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.timeoutInterval = 10

        let body: [String: Any] = [
            "productId": product.productId,
            "amount": 1,
            "consumeAll": false
        ]
        request.httpBody = try? JSONSerialization.data(withJSONObject: body)

        // Fire the request - silent failure on network/auth errors
        if let (_, response) = try? await URLSession.shared.data(for: request),
           let httpResponse = response as? HTTPURLResponse,
           httpResponse.statusCode == 204 {
            // 4. Optimistically update local UserDefaults
            optimisticallyUpdateProducts(products: products, defaults: defaults)
        }

        // 5. Reload widget timeline
        WidgetCenter.shared.reloadTimelines(ofKind: "QuickConsumeWidget")

        return .result()
    }

    /// Decrements the consumed product's amount or removes it if amount reaches 0.
    private func optimisticallyUpdateProducts(products: [WidgetProductItem], defaults: UserDefaults) {
        var updated = products
        if let first = updated.first {
            let newAmount = first.totalAmount - 1
            if newAmount <= 0 {
                updated.removeFirst()
                // Decrement the appropriate count
                if first.isExpired {
                    let count = max(0, defaults.integer(forKey: "ExpiringItemCount") - 1)
                    defaults.set(count, forKey: "ExpiringItemCount")
                } else {
                    let count = max(0, defaults.integer(forKey: "DueSoonItemCount") - 1)
                    defaults.set(count, forKey: "DueSoonItemCount")
                }
            } else {
                // WidgetProductItem is a struct with let properties, so rebuild
                updated[0] = WidgetProductItem(
                    productId: first.productId,
                    productName: first.productName,
                    totalAmount: newAmount,
                    quantityUnit: first.quantityUnit,
                    bestBeforeDate: first.bestBeforeDate,
                    daysUntilExpiry: first.daysUntilExpiry,
                    isExpired: first.isExpired
                )
            }
        }

        if let jsonData = try? JSONEncoder().encode(updated),
           let jsonString = String(data: jsonData, encoding: .utf8) {
            defaults.set(jsonString, forKey: "WidgetExpiringProducts")
        }
    }
}
