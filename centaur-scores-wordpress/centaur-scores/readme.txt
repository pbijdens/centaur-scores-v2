=== Centaur Scores ===
Contributors: centaurscores
Tags: archery, scores, results, live-scoring, shortcode
Requires at least: 6.4
Tested up to: 6.8
Requires PHP: 7.4
Stable tag: 1.0.2
License: GPLv2 or later
License URI: https://www.gnu.org/licenses/gpl-2.0.html

Embed live Centaur Scores match results and competition results in posts, pages and widgets, via shortcodes or blocks.

== Description ==

Centaur Scores lets you embed results from a [Centaur Scores](https://net42.org/) instance into any WordPress post or page:

* A single match's live results, via the `[centaur_scores_match]` shortcode or the "Centaur Scores Match Result" block.
* A competition's aggregated results, via the `[centaur_scores_competition]` shortcode or the "Centaur Scores Competition Result" block.

All communication with the Centaur Scores API happens on the server, from PHP. The API URL, username and password configured under **Settings > Centaur Scores** are never sent to the visitor's browser. Results are cached briefly to keep pages fast; if the API is temporarily unreachable, the most recently retrieved results keep being shown instead of an error.

The plugin follows the site's active language (English or Dutch) for all of its own labels and messages.

= Shortcodes =

Embed a single match's live results:

`[centaur_scores_match match="MATCH_ID" scope="SCOPE_NAME"]`

Embed a competition's results:

`[centaur_scores_competition competition="COMPETITION_ID"]`

Both shortcodes accept an optional `tenant` attribute (defaults to the tenant configured in the settings page), and an optional `class` attribute for a custom CSS class on the wrapper.

= Blocks =

The same two embeds are also available as block-editor blocks - "Centaur Scores Match Result" and "Centaur Scores Competition Result" - which preview live in the editor and support the standard block margin/padding controls plus a choice of 1, 2 or 3 result columns.

== Installation ==

1. Upload the plugin to the `/wp-content/plugins/` directory, or install it through the Plugins screen.
2. Activate the plugin.
3. Go to **Settings > Centaur Scores** and fill in the API URL, username, password and default tenant ID. Use the "Test authentication" button to confirm the credentials work.
4. Add the shortcodes or blocks to any post or page.

== Frequently Asked Questions ==

= Where do the API credentials come from? =

They are the same account credentials you would use to sign in to the Centaur Scores tenant-management web app.

= Does anything call the API from the browser? =

No. Every API call is made from PHP; the browser only ever receives the already-rendered HTML result.

== Changelog ==

= 1.0.1 =
* Clarified the API URL field's help text: it is the full base URL to prefix onto every API path, including any reverse-proxied /api segment your host requires.
* `build.sh <version>` now bumps the plugin's own version header, version constant, and readme Stable tag before packaging, instead of only naming the zip file.

= 1.0.0 =
* Initial release.
