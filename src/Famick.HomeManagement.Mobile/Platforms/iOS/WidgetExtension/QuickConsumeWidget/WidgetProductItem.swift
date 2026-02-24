import Foundation

/// Product data shared from the main app via App Group UserDefaults.
/// JSON is serialized from C# with PascalCase property names.
struct WidgetProductItem: Codable {
    let productId: String
    let productName: String
    let totalAmount: Double
    let quantityUnit: String
    let bestBeforeDate: String?
    let daysUntilExpiry: Int?
    let isExpired: Bool

    enum CodingKeys: String, CodingKey {
        case productId = "ProductId"
        case productName = "ProductName"
        case totalAmount = "TotalAmount"
        case quantityUnit = "QuantityUnit"
        case bestBeforeDate = "BestBeforeDate"
        case daysUntilExpiry = "DaysUntilExpiry"
        case isExpired = "IsExpired"
    }

    var expiryDescription: String {
        if isExpired {
            return "Expired"
        }
        guard let days = daysUntilExpiry else {
            return "No date"
        }
        switch days {
        case 0:
            return "Today"
        case 1:
            return "Tomorrow"
        default:
            return "in \(days)d"
        }
    }
}
