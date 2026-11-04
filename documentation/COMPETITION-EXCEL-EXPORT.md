# Competition Excel export

The competition results page offers **Export to Excel** (managers and administrators only, because the file
contains the full participant list). It downloads one workbook (`GET /api/competitions/{id}/export.xlsx?language=en|nl`)
with four sheets. Every sheet is an Excel table, so it can be sorted and filtered directly. Column headers follow the
user's language; the examples below are the English ones.

## Definitions

- **Score**: the raw match score (sum of all arrow values), never F1 points.
- **Avg per arrow**: score divided by the number of arrows actually shot. Empty when no arrows were shot.
- **Round result**: an archer is scored once per round. When they shot several matches in one round, the
  earliest-dated match is used (on the same date: the lower score), the same rule the results screen uses.
- **F1 points**: the F1-style points the archer earned in the round (12, 10, 8, 7, 6, 5, 4, 3, 2, then 1 for every
  lower position with a non-zero score), ranked within the competition's group-by categories. They are always
  calculated, even when no scoring rule uses F1.
- **Counting rounds of a rule**: the rounds a scoring rule actually uses: the best N by the rule's own aggregation.
  For an F1 rule these are the rounds with the most F1 points, so the counting score/average of an F1 rule is the raw
  score of *those* rounds.
- **Counting matches (competition)**: all rounds counted by at least one rule. A round counted by two rules is still
  one match.

## Sheet 1: Results

One row per archer that took part, in the same group and position order as the results screen.

| Columns | Meaning |
|---|---|
| Group, Position, Number, Name, Last name | Position is `-` for archers that do not qualify. |
| one column per category | The archer's category values. |
| per round: `R score`, `R avg per arrow`, `R F1 points` | The round result. Empty when the archer did not shoot that round. |
| per rule: `rounds shot` | How many of the rule's rounds the archer shot. |
| per rule: `total score (all)`, `avg per arrow (all)` | Over every round of the rule that the archer shot. |
| per rule: `counting rounds` | How many rounds the rule counted. |
| per rule: `points` | What the rule adds to the competition total: the sum of the counting scores, or of the counting F1 points for an F1 rule. |
| per rule: `counting score`, `avg per arrow (counting)` | Raw score and per-arrow average of the counting rounds only. |
| per rule: `minimum met` | Whether the archer shot at least the rule's minimum number of rounds. |
| Total points | The competition total (sum of the rules' points). |
| Counting matches, Counting score, Avg match score (counting), Avg per arrow (counting) | Over the competition's counting matches. |
| Qualified | Whether the archer met every rule's minimum. |

## Sheets 2 and 3: Data and Metadata

Exactly the participant list export for the participant list(s) used by the competition's matches: **all** archers
on the list, including those that did not take part, with their number, name, last name, active flag and category
values, and the Metadata sheet with the category values. Four columns are appended at the right:

| Column | Meaning |
|---|---|
| Avg match score (counting) | Average raw score over the archer's counting matches. Empty for archers that did not take part. |
| Avg per arrow (counting) | Per-arrow average over those matches. |
| Counting matches | How many matches the averages are based on (0 when the archer did not take part). |
| Qualified | Whether the archer qualifies for the competition. |

The workbook can be imported back as a participant list; the appended columns are ignored.

## Sheet 4: Details

One row per archer per match shot, including matches that were not used for the round (an archer in two matches of
the same round), for analysis.

| Columns | Meaning |
|---|---|
| Group, Position, Number, Name, Last name, categories | As on the Results sheet; categories as they were in that match. |
| Round #, Round, Round name, Match, Date | Where the score was shot. |
| Score, Arrows, Avg per arrow, F1 points | The match result. F1 points are those earned in this match. |
| Used for round | Whether this match is the archer's result for the round. |
| Counting in rules, Counting | Which rules counted this round, and whether it counts for the competition at all. |
| Qualified | Whether the archer qualifies for the competition. |
| Count X, Count 10, ... | How many arrows scored each key. |
| End 1, End 2, ... | Score per end. |
