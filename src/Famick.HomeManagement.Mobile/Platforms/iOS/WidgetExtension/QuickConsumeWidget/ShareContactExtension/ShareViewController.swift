import UIKit
import UniformTypeIdentifiers

class ShareViewController: UIViewController {

    private let appGroupId = "group.com.famick.homemanagement"
    private let urlScheme = "famick://import-shared-contact"

    override func viewDidLoad() {
        super.viewDidLoad()
        handleSharedContact()
    }

    private func handleSharedContact() {
        guard let extensionItems = extensionContext?.inputItems as? [NSExtensionItem] else {
            completeRequest()
            return
        }

        for item in extensionItems {
            guard let attachments = item.attachments else { continue }

            for attachment in attachments {
                if attachment.hasItemConformingToTypeIdentifier(UTType.vCard.identifier) {
                    attachment.loadItem(forTypeIdentifier: UTType.vCard.identifier, options: nil) { [weak self] data, error in
                        guard let self = self else { return }

                        if let error = error {
                            print("[ShareExtension] Error loading vCard: \(error.localizedDescription)")
                            self.completeRequest()
                            return
                        }

                        var vCardString: String?

                        if let data = data as? Data {
                            vCardString = String(data: data, encoding: .utf8)
                        } else if let url = data as? URL {
                            vCardString = try? String(contentsOf: url, encoding: .utf8)
                        }

                        if let vCard = vCardString {
                            self.saveAndOpenApp(vCardData: vCard)
                        } else {
                            self.completeRequest()
                        }
                    }
                    return
                }
            }
        }

        completeRequest()
    }

    private func saveAndOpenApp(vCardData: String) {
        guard let containerURL = FileManager.default.containerURL(forSecurityApplicationGroupIdentifier: appGroupId) else {
            print("[ShareExtension] Could not access App Group container")
            completeRequest()
            return
        }

        let fileURL = containerURL.appendingPathComponent("shared-contact.vcf")

        do {
            try vCardData.write(to: fileURL, atomically: true, encoding: .utf8)
        } catch {
            print("[ShareExtension] Error writing vCard: \(error.localizedDescription)")
            completeRequest()
            return
        }

        openMainApp()
    }

    private func openMainApp() {
        guard let url = URL(string: urlScheme) else {
            completeRequest()
            return
        }

        // Use the responder chain to open the URL
        var responder: UIResponder? = self
        while responder != nil {
            if let application = responder as? UIApplication {
                application.open(url, options: [:], completionHandler: nil)
                break
            }
            responder = responder?.next
        }

        DispatchQueue.main.asyncAfter(deadline: .now() + 0.3) { [weak self] in
            self?.completeRequest()
        }
    }

    private func completeRequest() {
        DispatchQueue.main.async { [weak self] in
            self?.extensionContext?.completeRequest(returningItems: nil, completionHandler: nil)
        }
    }
}
