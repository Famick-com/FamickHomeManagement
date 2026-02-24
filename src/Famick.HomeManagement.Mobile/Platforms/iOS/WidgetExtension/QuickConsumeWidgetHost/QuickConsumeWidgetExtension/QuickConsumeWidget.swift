import WidgetKit
import SwiftUI

@main
struct QuickConsumeWidgetBundle: WidgetBundle {
    var body: some Widget {
        QuickConsumeWidget()
    }
}

struct QuickConsumeWidget: Widget {
    let kind: String = "QuickConsumeWidget"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: QuickConsumeProvider()) { entry in
            QuickConsumeWidgetEntryView(entry: entry)
        }
        .configurationDisplayName("Quick Consume")
        .description("Scan to consume from inventory")
        .supportedFamilies([.systemSmall, .systemMedium])
    }
}

struct QuickConsumeWidgetEntryView: View {
    var entry: QuickConsumeEntry

    @Environment(\.widgetFamily) var family

    var body: some View {
        switch family {
        case .systemSmall:
            smallWidget
        case .systemMedium:
            mediumWidget
        default:
            smallWidget
        }
    }

    var smallWidget: some View {
        VStack(spacing: 8) {
            Image(systemName: "barcode.viewfinder")
                .font(.system(size: 36))
                .foregroundColor(.blue)

            Text("Quick Consume")
                .font(.system(size: 12, weight: .semibold))

            if entry.expiringCount > 0 || entry.dueSoonCount > 0 {
                HStack(spacing: 8) {
                    if entry.expiringCount > 0 {
                        Label("\(entry.expiringCount)", systemImage: "exclamationmark.triangle.fill")
                            .foregroundColor(.red)
                            .font(.system(size: 11))
                    }
                    if entry.dueSoonCount > 0 {
                        Label("\(entry.dueSoonCount)", systemImage: "clock")
                            .foregroundColor(.orange)
                            .font(.system(size: 11))
                    }
                }
            }
        }
        .padding()
        .widgetURL(URL(string: "famick://quick-consume"))
    }

    var mediumWidget: some View {
        HStack(spacing: 16) {
            VStack(spacing: 8) {
                Image(systemName: "barcode.viewfinder")
                    .font(.system(size: 40))
                    .foregroundColor(.blue)

                Text("Quick Consume")
                    .font(.system(size: 14, weight: .semibold))
            }

            Divider()

            VStack(alignment: .leading, spacing: 8) {
                if entry.expiringCount > 0 {
                    HStack {
                        Image(systemName: "exclamationmark.triangle.fill")
                            .foregroundColor(.red)
                        Text("\(entry.expiringCount) expired")
                            .font(.system(size: 13))
                    }
                }
                if entry.dueSoonCount > 0 {
                    HStack {
                        Image(systemName: "clock")
                            .foregroundColor(.orange)
                        Text("\(entry.dueSoonCount) expiring soon")
                            .font(.system(size: 13))
                    }
                }
                if entry.expiringCount == 0 && entry.dueSoonCount == 0 {
                    Text("All items fresh!")
                        .font(.system(size: 13))
                        .foregroundColor(.green)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding()
        .widgetURL(URL(string: "famick://quick-consume"))
    }
}

struct QuickConsumeWidget_Previews: PreviewProvider {
    static var previews: some View {
        Group {
            QuickConsumeWidgetEntryView(entry: QuickConsumeEntry(date: Date(), expiringCount: 3, dueSoonCount: 7))
                .previewContext(WidgetPreviewContext(family: .systemSmall))

            QuickConsumeWidgetEntryView(entry: QuickConsumeEntry(date: Date(), expiringCount: 3, dueSoonCount: 7))
                .previewContext(WidgetPreviewContext(family: .systemMedium))
        }
    }
}
