import WidgetKit
import SwiftUI
import AppIntents

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
                .containerBackground(.fill.tertiary, for: .widget)
        }
        .configurationDisplayName("Quick Consume")
        .description("Track and consume expiring items from your pantry")
        .supportedFamilies([.systemSmall, .systemMedium, .accessoryRectangular, .accessoryCircular])
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
        case .accessoryRectangular:
            rectangularWidget
        case .accessoryCircular:
            circularWidget
        default:
            smallWidget
        }
    }

    // MARK: - Home Screen Widgets

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

    // MARK: - Lock Screen Widgets

    var rectangularWidget: some View {
        Group {
            if let product = entry.nextExpiringProduct {
                rectangularProductView(product: product)
            } else {
                rectangularEmptyView
            }
        }
    }

    private func rectangularProductView(product: WidgetProductItem) -> some View {
        HStack {
            VStack(alignment: .leading, spacing: 2) {
                Text(product.productName)
                    .font(.system(size: 14, weight: .bold))
                    .lineLimit(1)

                HStack(spacing: 4) {
                    Image(systemName: "clock")
                        .font(.system(size: 10))
                    Text(product.expiryDescription)
                        .font(.system(size: 11))
                    Text("(\(String(format: "%.1f", product.totalAmount)))")
                        .font(.system(size: 11))
                        .foregroundStyle(.secondary)
                }
            }

            Spacer()

            if #available(iOS 17.0, *) {
                Button(intent: ConsumeNextExpiringIntent()) {
                    Image(systemName: "minus.circle.fill")
                        .font(.system(size: 22))
                }
                .buttonStyle(.plain)
            }
        }
    }

    private var rectangularEmptyView: some View {
        HStack(spacing: 6) {
            Image(systemName: "checkmark.circle")
                .font(.system(size: 16))
            VStack(alignment: .leading) {
                Text("Pantry")
                    .font(.system(size: 13, weight: .semibold))
                Text("All items fresh")
                    .font(.system(size: 11))
                    .foregroundStyle(.secondary)
            }
        }
    }

    var circularWidget: some View {
        let urgentCount = entry.expiringCount + entry.dueSoonCount
        return Group {
            if urgentCount > 0 {
                ZStack {
                    Image(systemName: "fork.knife.circle.fill")
                        .font(.system(size: 32))
                    Text("\(urgentCount)")
                        .font(.system(size: 10, weight: .bold))
                        .padding(3)
                        .background(Circle().fill(.red))
                        .offset(x: 12, y: -12)
                }
            } else {
                Image(systemName: "fork.knife.circle")
                    .font(.system(size: 32))
            }
        }
        .widgetURL(URL(string: "famick://quick-consume"))
    }
}

struct QuickConsumeWidget_Previews: PreviewProvider {
    static var previews: some View {
        let sampleProduct = WidgetProductItem(
            productId: "12345",
            productName: "Whole Milk",
            totalAmount: 2.0,
            quantityUnit: "L",
            bestBeforeDate: "2026-02-06",
            daysUntilExpiry: 2,
            isExpired: false
        )

        Group {
            QuickConsumeWidgetEntryView(entry: QuickConsumeEntry(
                date: Date(), expiringCount: 3, dueSoonCount: 7, nextExpiringProduct: sampleProduct))
                .previewContext(WidgetPreviewContext(family: .systemSmall))

            QuickConsumeWidgetEntryView(entry: QuickConsumeEntry(
                date: Date(), expiringCount: 3, dueSoonCount: 7, nextExpiringProduct: sampleProduct))
                .previewContext(WidgetPreviewContext(family: .systemMedium))

            QuickConsumeWidgetEntryView(entry: QuickConsumeEntry(
                date: Date(), expiringCount: 3, dueSoonCount: 7, nextExpiringProduct: sampleProduct))
                .previewContext(WidgetPreviewContext(family: .accessoryRectangular))

            QuickConsumeWidgetEntryView(entry: QuickConsumeEntry(
                date: Date(), expiringCount: 0, dueSoonCount: 0, nextExpiringProduct: nil))
                .previewContext(WidgetPreviewContext(family: .accessoryRectangular))

            QuickConsumeWidgetEntryView(entry: QuickConsumeEntry(
                date: Date(), expiringCount: 3, dueSoonCount: 7, nextExpiringProduct: sampleProduct))
                .previewContext(WidgetPreviewContext(family: .accessoryCircular))

            QuickConsumeWidgetEntryView(entry: QuickConsumeEntry(
                date: Date(), expiringCount: 0, dueSoonCount: 0, nextExpiringProduct: nil))
                .previewContext(WidgetPreviewContext(family: .accessoryCircular))
        }
    }
}
