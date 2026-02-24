import WidgetKit
import SwiftUI

struct QuickConsumeProvider: TimelineProvider {
    private let appGroupId = "group.com.famick.homemanagement"

    func placeholder(in context: Context) -> QuickConsumeEntry {
        QuickConsumeEntry(date: Date(), expiringCount: 0, dueSoonCount: 0, nextExpiringProduct: nil)
    }

    func getSnapshot(in context: Context, completion: @escaping (QuickConsumeEntry) -> Void) {
        let entry = loadEntry()
        completion(entry)
    }

    func getTimeline(in context: Context, completion: @escaping (Timeline<QuickConsumeEntry>) -> Void) {
        let currentDate = Date()
        // Refresh every 15 minutes
        let nextUpdate = Calendar.current.date(byAdding: .minute, value: 15, to: currentDate)!

        let entry = loadEntry()
        let timeline = Timeline(entries: [entry], policy: .after(nextUpdate))
        completion(timeline)
    }

    private func loadEntry() -> QuickConsumeEntry {
        guard let defaults = UserDefaults(suiteName: appGroupId) else {
            return QuickConsumeEntry(date: Date(), expiringCount: 0, dueSoonCount: 0, nextExpiringProduct: nil)
        }

        let expiringCount = defaults.integer(forKey: "ExpiringItemCount")
        let dueSoonCount = defaults.integer(forKey: "DueSoonItemCount")

        // Parse expiring products JSON for Lock Screen widget
        var nextProduct: WidgetProductItem? = nil
        if let jsonString = defaults.string(forKey: "WidgetExpiringProducts"),
           let jsonData = jsonString.data(using: .utf8) {
            let products = try? JSONDecoder().decode([WidgetProductItem].self, from: jsonData)
            nextProduct = products?.first
        }

        return QuickConsumeEntry(
            date: Date(),
            expiringCount: expiringCount,
            dueSoonCount: dueSoonCount,
            nextExpiringProduct: nextProduct
        )
    }
}
