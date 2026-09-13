In the web UI app, for narrowcasting reslt pages, I want predictable output regardless of the screen size.

There are a header and a footer to the narrowcasting page.
The header contains what it does now (logo, name, name of match, progress indicator)
The footer contains a progress bar for the countdown to an update or page switch

The parts of the page that are neither header or footer we will call the `result output div`

We want that div sized in such a way that (regardless of the screen resolution) it has place for 48 participants to a match, and 12 category headers.

I want everything in the `result output div` on a clear grid

A category header should take 1.0 grid height units
A result entry should use 1.0 grid height units and should internally have some minimal horizontal and verical padding.
Of the 1.0 grid height units for a result entry, roughtly 68% is used for the first line and 28% for the second line, and at most 4% is used for padding.

When in portrait mode the `result output div` should have two columns
When in landscape mode the `result output div` should have three columns
The `result output div` should vertically fill all available space.
A column should be full before a next column is used.

A result page must without scrolling have space for 48 participants, plus *all* category headings used in the output for the first 48 participants. The grid height is adapted for that, so a match with 48 participants and no matter which number of participants will always fit.

If there are more than 48 results, then the result page will be made to allow scrolling *horizontally* adding as many columns as needed to display all results and headings. Throughout the update time, the page will automatically scroll to the right, so it starts at 0% horizontal scroll and if the update time is 15 seconds, then after 15 seconds it will have reached 100%.

Font sizes shall be adjusted dynamically based on the calculated grid height for the fonts to vertically use up all space they have available.
