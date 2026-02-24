import WidgetKit
import SwiftUI

struct QuickConsumeProvider: TimelineProvider {
    private let appGroupId = "group.com.famick.homemanagement"

    func placeholder(in context: Context) -> QuickConsumeEntry {
        QuickConsumeEntry(date: Date(), expiringCount: 0, dueSoonCount: 0)
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
            return QuickConsumeEntry(date: Date(), expiringCount: 0, dueSoonCount: 0)
        }

        let expiringCount = defaults.integer(forKey: "ExpiringItemCount")
        let dueSoonCount = defaults.integer(forKey: "DueSoonItemCount")

        return QuickConsumeEntry(
            date: Date(),
            expiringCount: expiringCount,
            dueSoonCount: dueSoonCount
        )
    }
}
