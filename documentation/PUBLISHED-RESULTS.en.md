# Publishing results on a public page

> 🇳🇱 **[Nederlandse versie → PUBLISHED-RESULTS.nl.md](PUBLISHED-RESULTS.nl.md)**

You can publish the results of a match or a competition on a public web page, for example to share them with your
club members, in a newsletter or on social media. Anyone with the link can see the results **without logging in**.
This guide is for managers and administrators.

## Contents

- [What visitors see](#what-visitors-see)
- [Publishing a match](#publishing-a-match)
- [Publishing a competition](#publishing-a-competition)
- [Choosing a slug](#choosing-a-slug)
- [Results of matches that are still active](#results-of-matches-that-are-still-active)
- [The public match page](#the-public-match-page)
- [The public competition page](#the-public-competition-page)
- [Privacy and search engines](#privacy-and-search-engines)
- [Stopping publication](#stopping-publication)
- [Frequently asked questions](#frequently-asked-questions)

## What visitors see

A published page lives at a fixed address of your choosing:

```
https://<your-centaurscores-address>/results/<slug>
```

For example `https://centaurscores.nl/results/club-championship-2026`. The page shows the name, the date(s) and your
organisation's logo, followed by the results. Visitors can switch between English and Dutch, and between results
**By category** and **All participants**. The page works on phones as well as on large screens and is refreshed
each time a visitor reloads it.

Only the position, federation number, name and scores are shown. Other participant details, such as category values
or contact information, never appear on the public page.

## Publishing a match

1. Open the match and choose **Edit match settings**.
2. Scroll down to **Publish results**, below the live scoring scopes.
3. Tick **Publish the results at slug** and type a slug, for example `club-championship-2026`.
4. Leave **Include results of matches that are still active** ticked if visitors may see the scores while the match
   is still running (see [below](#results-of-matches-that-are-still-active)).
5. The **Public link** appears below the setting. This is exactly the address that will be used.
6. Click **Save** at the bottom of the page.

If the slug cannot be used, you get a message and **nothing on the page is saved**: correct the slug and save again.

## Publishing a competition

1. Open the competition.
2. Scroll to **Publish results** at the bottom of the page.
3. Tick **Publish the results at slug**, type a slug and choose whether to include active matches.
4. Click the **Save** button of that section. A message right below it confirms the publication or explains what
   went wrong.

The rest of the competition (rounds, matches, scoring rules) is saved separately as before; this button only saves
the publication.

## Choosing a slug

The slug is the last part of the public address. You can type it the way you like; CentaurScores turns it into a
tidy address automatically, and the **Public link** shows you the result while you type:

| You type | Address becomes |
|---|---|
| `Club Championship 2026` | `/results/club-championship-2026` |
| `Wintercompetitie – Ronde 3!` | `/results/wintercompetitie-ronde-3` |
| `Kampioenschap Zuid-Holland` | `/results/kampioenschap-zuid-holland` |

The rules: everything becomes lowercase, accents are removed (é → e), spaces and punctuation become a single dash, and
the address is at most 100 characters long.

Each slug can be used **once across all organisations**, by one match or one competition. If someone (in your
organisation or another one) already uses it, you see *"This slug is already used by another match or competition.
Choose a different one."* Add something distinctive, such as your club's name or the year.

> **Tip:** a slug that cannot be guessed easily (for example including the year and your club name) keeps casual
> visitors away from results you only meant to share with a small group. See also
> [Privacy and search engines](#privacy-and-search-engines).

## Results of matches that are still active

The option **Include results of matches that are still active** is switched on by default.

- **On:** scores appear on the public page while the match is still being shot, so supporters can follow along. The
  page states that it includes provisional results, for example *"Includes provisional results of 1 match(es) still
  in progress."*
- **Off:** results only appear once the match has been closed.
  - On a match page, visitors see *"This match is still in progress. The results will appear here once it has
    finished."*
  - On a competition page, the active matches are left out: those rounds show `-` and the totals are calculated
    without them. A notice tells visitors how many matches are not included yet.

A match counts as active as long as it is open for scoring. Close the match once all scores are final.

## The public match page

- **All participants:** one ranking of everyone in the match, in the same order as the results on the narrowcast
  screen (including tie-breaker rules). Columns: position, federation number, name, number of arrows, average per
  arrow, and score. An asterisk (\*) marks a tie decided by a tie-breaker.
- **By category:** the results are grouped in the same way as the match's **live scoring scopes** (the narrowcasting
  settings). Visitors see the **category names** (for example *Recurve – Seniors*), never the name of the scope.
  When several scopes group by the same categories, that grouping is shown only once.

So, to control how the grouped view looks, set up the live scoring scopes of the match. A match without live scoring
scopes only offers **All participants**.

## The public competition page

- **By category:** the groups and positions are the same as on the competition results screen, following the
  categories the competition groups its results by. If the competition has no grouping, only **All participants**
  is offered.
- **All participants:** one list of everyone, sorted by average per arrow (highest first), then by federation number
  (lowest first), then by name. Archers with the same average share a position.

Both views have the same columns:

| Column | Meaning |
|---|---|
| **#** | The position. `-` means the archer does not (yet) qualify, for example because they did not shoot the minimum number of rounds. |
| **Fed. no.** | Federation number. |
| **Name** | The archer's name. |
| **Avg/arrow** | Average per arrow over all matches that count towards the result. |
| **Match scores** | The score of every round. `-` means the archer did not take part. A ~~struck-through~~ score was shot but does not count towards the total (for example because only the best rounds count). |
| **Total** | The competition total. |

**With F1 scoring**, the F1 points are shown in brackets: per round as `278 (12)`, and the total column reads
`1104 (46)`: the total of the match scores, followed by the number of F1 points earned. The column is then called
**Total (F1 points)**.

On a computer the round scores are shown in neat columns under the round names. Long round names are shortened at
the front, so the distinctive end remains visible (*"…ronde 12"*); hovering over the name shows it in full. On a
phone the round scores are stacked on top of each other, each with its round name.

## Privacy and search engines

- Published results are **public**: anyone who has, or guesses, the address can view them, without an account.
- Search engines (Google, Bing, …) are explicitly asked **not to index** these pages, so they should not show up in
  search results. A link you share yourself, for example on social media, can still be found through that post.
- Only position, federation number, name and scores are shown.
- To prevent abuse, the number of times a single visitor can load results in a short time is limited. Normal use,
  including refreshing a few times, is not affected. Someone who reloads very often in quick succession first gets
  slower responses and then the message *"Too many requests in a short time. Please wait a moment and try again."*
- The page may lag behind the actual scores by up to about 15 seconds.

Do you not want results (or certain participants) to be public? Then do not publish, or ask the participants for
permission first, in line with your club's privacy policy.

## Stopping publication

Untick **Publish the results at slug** and save. The address stops working immediately (visitors see *"No results are
published at this address."*, at most 15 seconds later), and the slug becomes available again for another match or
competition.

Deleting a match or competition also removes its publication.

## Frequently asked questions

**Can I change the slug later?**
Yes. Type a new slug and save. The old address stops working, so update any links you have already shared.

**Can a match and a competition use the same slug?**
No. A slug belongs to one match or one competition.

**Can I publish a match that is part of a competition, and the competition itself?**
Yes, each with its own slug.

**Do I need to republish when scores change?**
No. The page always shows the current results. Visitors only need to reload the page.

**Who can set up a publication?**
Managers and administrators.
