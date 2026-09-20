# Signing scorecards

## Introduction

In the real world, after an archery match is finished, the participants will get together and
they will physically put two signatures on every score card: one from the archer and one from
the marker. This concludes the match and is a requirement for the score to be accepted as a
result for the match. The archer signature signifies approval, the marker signature signifies
that the scorecard was verified for correctness by a different person.

## The requirements

### General

In the software, we need to mimic this approval, however we are going to offer this in three
flavors:
1. There is no need to sign a scorecard at all
2. The organizer can request the scorecards to be signed by a simple button press+acknowledge action
  where at the bottom of the scorecard a button is pressed "Sign" or "Ondertekenen" that opens a
  modal pop-up showing the text "After signing a scorecard, the scores are final and can't be altered
  anymore. Scorecards should only be signed with approval from the archer, after verifying that the
  scores have been fully and correctly filled out." with a "Confirm" / "Cancel" button.
3. The organizer can request the scorecards to be signed by pressing the "Sign" button, after which
  both the archer and marker must physically draw a signature (split screen, two clearly marked areas
  stacked each taking up horizontally all of the screen horizontally and with an aspect ratio of 5:1
  per field or 5:2 for the stack).

In the web ui, in the match configuration view and in the match templates there will be an additional
opion in the first block where the user can select one of the three signature modes: "No signature 
required" (default), "Request confirmation", "Require signature". This is of course persisted in the
backend.

When signing a scorecard is required, then in the web ui, on the match details page where the
list of participants is shown, the system will also show per participant if the scorecard was
already signed. If the user has Manager rights, they can also toggle this state on or off. This
will always require confirmation from the user so there is no accidental conformation. The
toggle can be in the same block/panel as the status display and will not require an additional
save when used (it will refresh the view).

The match participant list in the web ui can be grouped by signed and unsigned, where the 
unsigned category will in that case be rendered first. This is an additional option in the existing
group by menu.

In the web ui the match participant detail view will clearly show if a scorecard is in Signed
or Unsigned condition using a status bar at the top of the page: "This scorecard was not yet signed"
and "This scorecard is signed and final". If the match does not require signing the scorecard
this is not shown.

In the web ui, when a scorecard is signed, and when the scores are expanded, if there are
signatures recorded, these are shown there.

In the match CSV export there will be a column indicating if the scorecard was signed, called either
Signed (en) or Ondertekend (nl) and it will contain a Yes/Ja or No/Nee value.

In the web ui, if the user is a manager, they can alter scores on signed scorecards.

In the "centaur-scores" flutter application and in the "mobile-web-scoring" application(s) on the
score_card below the last line of scores there will be:
1. If the scorecard was not signed yet, be a "Sign" button. When a scorecard is activated (receives 
   focus by any means including next) and when all scores on the card are filled in, this will
   scroll into view and get focus, and the score keyboard will not be visible. The button 
   will also be there when the scorecard is incomplete, since it's not impossible for matches to be
   stopped early because of e.g. bad weather. The buton will act as described above.
2. If the scorecard was signed this will either show the signatures of the archer and marker, or
   it will show a text block "This scorecard is signed and can no longer be altered".

In either case, after the scorecard is signed it can indeed no longer be altered. If no signature
is required in the settings this button will not be shown and the card will never be read-only and
signatures will never be shown in those two applications.

### Printing scorecards

A feature is added to the web ui software where scorecards can be printed. In that case, when the
option to print the scorecards is selected from the match ellipses menus the system will create a
printable sheet. On that sheet there wil be in a grid all scorecards for the match, where a scorecard
starts with a "form" showing: The logo for the tenant and the name of the match, the date of the match,
the optional additional line of text (see below), the federation number of the archer, and in
alphabetical order the categories registered for the archer in the participant list.

Note: Before creating the printable list, the system will show a page where the organizer can select 
the categories to be printed, and they can enter an optonal line of text to be added below the name
of the match, e.g. "Bij AHV Centaur te Amersfoort". Also the organizer can using checkboxes select
for which match participants to add the scores (or clear/set them all with a 3-state master checkbox 
at the top)

Below the participant/match details form, there is a form added with headings End, then starting at "1" 
a column per arrow, Score, Subtotal followed by one row per end where the end numbers are 
printed borderless, and the rows will have one bordered grid cell per end for the arrows, sub, total, 
10s and 9s. For all ends already shot, these will be filled in with the recorded value(s). Otherwise
the cells will be rendered empty and large enough for a normal human being to write in... The borders
can be rendered at 50% black to save ink and the planet.

Below the end scores there will be a place to write down the total (or where it's printed if known
and there is more than one arrow value available for the card)

If the scorecard is signed, below that are either the signatures or an indication that the card is signed.

If the scorecard is not signed, below that will be a line for the marker and one for the archer to sign the card in.

If on the page verically we can stack two cards, then do so; also add as many columns as make sense.

## System requirements

On the flutter and mobile scoring apps, signing a card should also be stored locally until it can be
sent to the server, just like scores. There is no need or wish for any conflict resolution though.

The API powering the anonymous score entry should refuse to update any score card that is signed 
already. This limitation should not just be a UI limitation.

The full API should allow editing signed score card as well as allow withdrawing signatures.

Signatures can be stored in the database as a black and white low resolution drawing. The aspect ratio
os f a single signature is 5:1 and even at 300dpi the image would be less than 64KB, but I would guess 
that 72 dpi is sufficient and the image can be stored compressed in any format and should probably fit
in a database column.