# Changelog

Release notes for the self-hosted Famick Home Management server, published as the Docker image
[`mtherienfamick/homemanagement`](https://hub.docker.com/r/mtherienfamick/homemanagement).

## 1.0.0 — 2026-11-06

The first stable release. It follows `1.0.0-beta.13` (June 2026), so everything below is new to
anyone running a beta image.

### Before you upgrade

Back up your database. The server applies its database migrations automatically on start, and
this release carries several months of them.

- **Every user needs their own email address.** A sign-in email now names one person, compared
  without regard to case or surrounding spaces. If two users share one (`Pat@Example.com` and
  `pat@example.com` count as the same), the upgrade stops and the server won't start. The log
  names the addresses: *"More than one user has the email address(es): … Give each a distinct
  address, or remove the extra users, then restart."* Change or remove the extra users and
  restart. Nothing has been changed at that point.
- **Set `BASE_URL`.** Links the server emails out — verification, password reset, data export —
  are now built from `BASE_URL` in your `.env` (see `.env.example`). Set it to the address people
  use to reach your server, e.g. `https://famick.example.lan`. Left empty, the server falls back
  to the address of whichever request triggered the email.
- **You may need to sign in to the web app once.** The browser now keeps its session in an
  HttpOnly cookie that has a separate name for HTTP and HTTPS. A browser on HTTPS will ask you to
  sign in again after the upgrade; one on plain HTTP carries on.
- **Vehicles are now equipment.** Existing vehicles move to the Equipment list as a *Vehicle*
  kind, keeping their maintenance records, mileage and drivers. They no longer have a separate
  page.
- **The web app sends a Content-Security-Policy.** If your reverse proxy injects scripts into
  pages (analytics, for example), the browser will block them.
- **Home Assistant add-on users:** update the add-on once a version built on 1.0.0 is published.
  It is rebuilt from this image.

### The Famick app

The iOS and Android app is now **Famick** on the App Store and Google Play, and it connects to a
self-hosted server as well as to the cloud. Its source has moved out of this repository. Run
this release, or a later one, with the 1.0 app: earlier beta images don't have some of the
endpoints it uses.

### Recipes

- **Import a recipe from a web page.** Paste a link on the Recipes page. Famick reads the recipe,
  matches each ingredient to one of your products, and places it on the step that uses it. A line
  it can't match keeps its original text, and you can link it to a product later. If you import a
  page twice, Famick offers to open the existing copy instead.
- **Rate recipes** from one to five stars, in half stars. Each household member rates separately,
  and you can sort and filter by the household's average or by your own rating.
- **I Made This** takes a recipe's ingredients out of stock, and shows any line that falls short
  before you confirm.
- **Cook mode** on the web shows one step per screen in large text.
- Cooking times written in step instructions, such as "simmer for 20 minutes", are recognized,
  and the app can start a timer from them.
- The recipe page has a **consolidated ingredient list** that combines repeated ingredients
  across steps.
- Ingredient search lists generic products before specific brands.
- Step instructions are optional, so a step can be just a title and its ingredients. The step
  editor keeps unsaved edits and adds a new step below the list.
- **Add missing to shopping list** counts what's already on the list, buys whole purchase units,
  and leaves out lines whose unit can't be compared with stock, with a note to check them by hand.

### Units and stock

- **Purchase and stock units** are applied throughout. Moving bought items from the shopping list
  into stock converts purchase units to stock units with the product's factor, and divides the
  price per stock unit. Child products follow their parent's stock unit. Existing stock amounts
  are never reinterpreted.
- **Standard volume and weight conversion.** Tag a unit as a standard measure (cup, tablespoon,
  gram, ounce …) and recipe stock checks convert between them.
- **Dash** and **Pinch** units are added, including to existing households.
- Recipe stock checks count child products' stock, and share a product's stock across the lines
  that use it.
- You can clear a stock entry's best-before date or location, and edit entries from the product
  page.
- A parent product holds its own stock rather than only totalling its children's.

### Products and catalog

- The built-in product catalog has refreshed icons and seed data, and updates itself when a new
  release ships changes to it.
- Product search covers your own products and the store together, with fixes for scanning
  by-weight barcodes.
- Barcode lookups are faster and tell a store that's busy apart from one that failed.
- The grocery setup wizard has been removed.

### Household and accounts

- **Invite people** to your household by email.
- **Download your data.** Settings → Download your data builds an archive of your household and
  emails you a link to it.
- **Delete your account,** or your whole household, from the web app or the Famick app. A member
  who leaves can first be emailed a copy of their own data. Contacts they kept private leave with
  them, and shared ones pass to an admin.
- An admin resetting a member's password now emails that member a reset link.
- Admins can no longer change a member's sign-in email.
- A self-hosted server unlocks every feature. It doesn't use cloud subscription plans.
- A server that holds a single household turns away new sign-ups.
- The setup wizard detects your time zone, stores it on the household, and saves heating, air
  conditioning and water-heater details.
- Deleted accounts are purged on a schedule.

### Contacts and calendar

- Each contact has a visibility: private, shared with chosen members, or shared with the
  household. Its owner manages who can see it.
- Calendar and contact feed links belong to the member who created them, and stop working when
  that member leaves the household. Existing feed links keep working.
- Recurring events are validated, and calendar queries cover bounded date ranges.

### Notifications and email

- Reminders reach the Famick app from a self-hosted server without push notifications. The app
  downloads upcoming reminders (calendar, expiring stock, low stock, tasks, chores, maintenance)
  and shows them on schedule, even offline. Expiring stock arrives as one daily digest.
- Notification emails are easier to read on a phone, and support dark mode.

### Sign-in and security

- Passkeys work in the Famick app, and a new account can be created with a passkey alone.
- Where Sign in with Apple or Google is configured, the server checks each provider token against
  the provider's published keys, and binds it to the sign-in attempt that requested it.
- Repeated wrong passwords lock the account for a short time. Sign-in answers the same way
  whether or not an account exists.
- Every session has its own refresh-token family. Sign-out works even after the access token has
  expired.
- One password rule applies wherever a password is set.
- Linking a sign-in to an existing account by email requires an email the provider has verified.
- The web app sends browser-hardening headers and a Content-Security-Policy.
- URL fields accept only `http` and `https` links. Images fetched from a URL, and calendar
  subscriptions, go through an outbound client that doesn't reach internal network addresses.
- Paging and result-count parameters have upper bounds.

### Under the hood

- .NET 10 with current Microsoft packages, and centrally managed package versions.
- Stored files are cached by the browser, and their downloads share a bounded connection pool.
- The web app persists dark mode and applies it before sign-in.
