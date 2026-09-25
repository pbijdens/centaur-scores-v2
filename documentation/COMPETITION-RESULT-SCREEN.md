In the web-ui app, when rendering  the competition results for printing the output takes way too much space. What I need in the printed results is a much more compact format.

My requirements:
1. On both landscape and portrait the output is in two columns
2. There is one report-header with on it:
   1. tenant logo and name of the competition
   2. current date
   3. dates (start, end) of the competition
3. The report is either (mode 1) a continuous stream, splt in roughly half to divide it over two columns that continue running over multiple pages if need be, OR (mode 2) a more traditional 2-column output where you would read the results page by page, ; this can be selected on the preview screen as a rendering mode, and when switching the system will re-render or switch stylesheets. The selection itself is never rendered of course in the printed report...
   1. A column ideally never ends with a header row, there will always be at least one participant row following any header; for the more classic mode this is allowed to happen because we probably cannot prevent this.
   2. it is fine when a column starts with the last result from the previous group (as an orphan)
   3. The result-stream consists of groups
      1. each group starts with a header, followed by the ranked participants
      2. a header row and a participant row are equally high
      3. the header row will have some internal whitespace in it to give some air to the report
      4. a participant row has 3 main elements
         1. left is the ranking number (or - if not ranked or disqualified)
         2. right is the score
         3. in the middle there are 1, 2 or 3 lines
            1. line 1 is in semibold the participant's name
            2. line 2 and 3 are in a (much?) smaller font the individual parts used by the scoring rule(s) meaning:
               1. a sequence of the scores achieved in all rounds per rule
               2. if a no score was achieved in a round a dash or any other symbol that properly signifies that
               3. if a score was achieved but it is indicated as not used in the result it's strikethrough
               4. the block for a scoring rule is prefixed/labeld with the name of the scoring rule. so I might get something like 18 meter: 100 - - - 90 - 120, 25 meter: 70, 70, 70, 70, 70, -60-, -40-
4. Mode 1 is to be pinned to a notice board, mode 2 is for sending out printed PDF results.

