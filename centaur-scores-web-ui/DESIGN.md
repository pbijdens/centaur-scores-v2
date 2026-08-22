## Management UI

The management UI is a very simple front-end that offers the following functionality, on top of what's designed in the root project design documentation [DESIGN.md](../documentation/DESIGN.md)

### Requirements

1. The management UI is very simple and clean and easy to maintain
2. The management UI makes use of as many standard browser controls as possible
3. The management UI is written in Svelte with TypeScript
4. The management UI is mobile friendly
5. The management UI will have a top-level menu (may be in a hamburger menu or in a bar at the top of the screen)
   1. When in a bar, it will contain the logo and name configured for the current tenant; clicking that will take the user back to the home screen.
6. The live scoring pages do not show a top-level menu or similar header 
7. All times stored in the database and communicated on the API are UTC
8. All times stored in and read from the UI are local times
9. When a date is selected or modeled it's timezone agnostic, meaning that it's that date in ALL timezones

### Use cases

#### UC1 - Language selection

As a user, at any point in time I can switch display langauge for the application. I can switch between English and Dutch using a simple drop-down initiated via the top-level menu. The system will remember my last chosen language in local storage and will use that again the next time I use the application.

#### UC2 - User is not logged in

As a user, when I am not logged in, all I get to see is a login screen.
On the login schreen I can via a dropdown select the tenant I want to log into.
If I have previously logged in, the system will have stored the last tenant I logged into in the browser's local data and will automatically select that for me
On the login screen I enter my username and password.
Having done that the Login button is enabled and I can press that button to log in.
If login is succesful, I will be taken to the Tenant Home screen.
If login fails, an error is displayed in the login box indicating that login failed and the focus moves to the username field.
After login, my username will be shown in the top-level menu, as well as a log out button.

Technically, when I am logged in the system will store the active bearer token in the local storage. The system can check that token for validity to determine if I am logged in. When at any point in time the token gets rejected by the backend, the system will delete it and send me back to the login screen. Token refresh is not available yet.

#### UC3 - User wants to log off

As a user, when I want to log off in I can access a function in the top-level menu to log off. This will remove the saved bearer token and refresh, causing me to be directed back to the login screen.

#### UC4 - Tenant home screen

As a user I can see and manage tenant data from the tenant home screen. On the tenant home screen I have the following sections available:
1. A clickable list of "Currently open matches" showing all matches that are active now
   1. Clicking a match will take me to the Match's home screen
   2. There is a "deactivate all" button that will toggle all these matches to inactive
2. A clickable list of "Upcoming matches"
   1. A list, sorted on date, with matches configured for today and the next few weeks
   2. Clicking a match will take me to the Match's home screen
   3. There is a "see all" button that will take a user to a screen with a clickable list of all past, present and future matches for the tenant
3. A clickable list of active competitions
   1. A competetion is active when the date range (partly) overlaps with today
4. A clickable list of active participant lists
   1. With a "see all" button to open a the "lists of participants" management screen
5. A function-button container or menu for
   1. Managing categories
      1. Clicking this shows the UC5 category management screeen
   2. Managing match templates
   3. Managing lists of participants
   4. Managing matches
   5. Managing competitions
   6. Managing sub-tenants, provided I am tenant administrator for this tenant

#### UC5 Category management

As a user, when Iam logged in and have sufficient rights, from the Tenant Home screen I can select a function to manage categories this takes me to the category list.
That list is ordered alphabetically by default. There is a button above the list that allows me to add a category.

Opening any category allows me to Add/Remove values or even delete the category if it's not used.

Once an category is used by a match it can't be deleted anymore, otherwise there is a "Delete this category" button.

#### UC6 - User wants to set up a match template

As a user, from the Home screen I can select a function to manage match templates.

This will take me to a page that lists all match templates I have created for my tenant. These are listed in alphabetical order, and on the list I see the template name, the participant mode and the participant list's name if set.

This will take me to a page where I can then set up a match template. I can:
- Set a name for the template (e.g. CLubcompetitie 18 meter 3 pijl)
- Select a participant list that participants should come from
- Set a participant-selection mode for the score entry devices
  - Restricted to match
  - Restricted to a specific source list
  - Restricted to a specific source list, allowing adding unlisted participants
  - Only unlisted (ad-hoc match)
- Select the catgeories for this type of match and place them in the correct order
- Define the scoring "keyboard"
- Define an ordered list of standard score-entry devices that are created with each match using the template
- Define per category rules for which keyboard elements are disabled
  - e.g. For the categorynode "Compound" from the "Discipline" category the value kys with ID '5', '4', '3', '2', and '1' are not available.
- Define the scoring rules for calculating the match result
- Define the live scoring scopes

#### UC7 - Managing lists of participants

As a user I can navigate ot the participans list management screen by pressing the button on the tenant homepage. As a user the participant list screen allows me to CRUD lists of participants.

Participant lists have as metadata a name and an indication of whether they are still active.

Inactive participant lists are only shown in the full list we see when we click the function button or when we go via "Show all". The list is sorted so active lists are on the top, next by name. Inactive participant lists are rendered visually different, but are clickable nonetheless, opening their details and are later in the display order than the rest.

Clicking a list takes me to the details screen that allows me to edit the list's metadata and to manage the participants on the list. Participant list details consist of a metadata editor, and a list members editor. Users with sufficient rights can edit and save the metadata. The participants list shows all participants in the list, ordered by last name. Participant lists in context of matches always show all relevant category values also, where the relevant values are the ones identified in the match definition, in the order identified there. So we could have Pieter-Bas IJdens (Recurve/Klasse C) in the participant list when I need to select an archer.

Clicking (or touching) a participant shows a screen with:
- Their metadata as defined in the model, editable when the user has sufficient privileges
- The current value for each category, with the possibility to change it if the user has the rights.
- Whether they are still active or not (inactive participant list members are not shown in selection lists, they are shown in results and other lists)

In order to save, all values should be filled in.

From the single participant editor it's easy to return to the participant list. Goiing to the tenant homepage is also trivial.

#### UC8 - Managing matches

Selecting that function, or selecting show all will take the user to the list of matches for the tenant.

The list shows for each match the name, date and number of participants currently assigned.

The list will be ordered such that the currently active matches are always on top, rendered visually different (active)

Next are all current and future matches not shown yet, ordered by date, ascending.

Next are all past matches; visually different ordered by date, ascending.

Above the list there is a toolbar with options to add a match and a filter to filter the list on "Only future matches" or "Only past matches". There is a text field that can be used to filter the list (on name)

Alongside the filter options for the list of matches , there is an option to add a new match to the system.
When adding a new match, the user must enter a name and a date, and optionally can select a template or can define the match from scratch. Selecting a template will pre-configure all templated options and then take the user to the single-match view. Not selecting a template will take the user to the match editor and the user can define all match settings ad-hoc.

Clicking or touching a match from the list of matches navigates to the matches homescreen.

On this screen the user can, given sufficient privilege:
- Activate or de-activate the match
- Open a printable screen with QR codes encoding the URLs for all scoring devices, with LARGE QR codes placed in a bordered block with in that block the name of the match plus the name of the scoring device the code is for. At most 4 codes should fit on an A4.
- See the match metadata and update it.
- See all participants in a list (remember: Participant lists in context of matches always show all relevant category values also, where the relevant values are the ones identified in the match definition, in the order identified there. So we could have Pieter-Bas IJdens (Recurve/Klasse C) in the participant list when I need to select an archer.)
  - That list can be shown sorted by name, or sorted by score
  - That list can be grouped by the match-categories, or grouped by the assigned "Score Device" (there is one Unassigned group at the end of the list)
- Add participants
  - Either select from the list
  - Or add details (name, lastname, categories, federation number)
- Touching a participant opens their details. Allowing to:
  - select a different participant from the list
  - or remove the selected participant and enter details manually for a local participant
  - see all entered score values (one line per end, then total per end, then subtotal, then subtotal for group, 
  - edit all entered score values
  - quick-set a 'total' for an archer; system will auto-fill the score values by equally dividing the lowest possible (last in the keyboard list) values

Because of the vast amount of options available, the match's main screen is the full list of participants (see specs) and there are function buttons at the top of the page to edit the match metadata, edit the scoring devices, see the scoring device QR codes, and see the results.

The match metadata screen is like the match template screen also allows assigning users as match admins.

The results function is a dropdown button, with a dropdown value per configured result scope, taking the user to the full results link for the match, including the scope.

#### UC9 - Exporting matches

Matches can be exported to CSV, where each row contains:
federation number, full name (or last name if full name not set), (next, one category value per column), score group 1, ..., score group n, total, equalizer 1, equalizer 2, ...

Export is initiated via a button on the match homepage.

#### UC10 - Deleting a match

tenant admins can delete matches from the match homepage. This does require confirmation.

#### UC11 - Managing competitions

Selecting the function will take the user to the list of competitions.

The order of that list is such that the currently relevant competitions are at the top of the list, visually different from future competitions (shown next) and past competitions (shown last); when start dates overlap, sort order is secondary by name.

I don't see a reason yet to filter the list, but should for consistency allow filtering on past, active, future and allow searching on title, as we do for matches.

Selecting a competition takes the user to the competition's homepage.

A user can add a competition by provided the competition's name, start date and end date. After choosing to add the competition, the user is taken to the competition's homepage.

#### UC12 - Deleting a competition

Tenant admins can delete competitions from the competition homepage. This does require confirmation.

#### UC13 - The competition homepage

Show the list's metadata and allows editing this and storing the result.
- When the date(s) change it may be required to re-load the list of qualifying matches.

Shows the list of rounds, in the order defined in the database.
- Allows adding a round, deleting a round, editing a round and re-ordering a round.
- When adding or editing a round, the user can change the round's long name and short name.
- The round list also shows the matches assigned per round.
- We'll use the same style of list for the rounds as we used for devices in the match view.
- We'll follow the same pattern for adding matches to a round that we followed for adding match participants to devices, so we have an add butotn that toggles showing a match selection dropdown. In that dropdown matches are sorted by their date(s), newest first. We'll only show matches that have a date that falls within the competition period.
- Note that matches may be added to the same competition multiple times (in different contexts)

Shows the grouping configuration
- Allows configuring the categories that are used for grouping the competition results by.

Shows the list of scoring rules that are in place
- Rules can be added or removed
- Rules can be re-ordered
- A rule has a name
- A rule refers to a subset of all rounds defined (RoundIdsJson)
- In HighestScores it defines the number of scores that are added to calculate the total score. If there are more than this many non-zero scores in the included rounds, it uses only this many for the calculation. In the result-list the scores that havenot been used are rendered in a strikethrough format.
- In MinimumScores it defines the minimum number of scores a user should have to qualify for the results at all. Participants with less scores than this will be disqualified for the competition; their scores will be included in the result list, but they will not be assigned a position and will be placed in the results below the lowest qualifying participant in their group.
- Only participants that are actually in a participant list are included in the results. It does not really matter which list, but ad-hoc participants are not part of the competition result. 
- In Aggregation it defines how the score for a round is calculated. 
   - When Aggregation is "f1" then to calculate a round score:
      - Per match, the results of all participants that match the competition's grouping are selected
      - These are sorted as they are for UC17, including the tiebreakers
      - Then each archer that ends on #1 position gets 12 points
      - ... each archer that ends on #2 position gets 10 points
      - ... each archer that ends on #3,#4,#5,#6,#7,#8 and #9 position gets 8,7,6,5,4,3 and 2 points respectively
      - ... all remaining archers with a non-zero score get 1 point
      - intentionally saying each archer because two archers may be on #1 position, in hwich case we'll skip #2
      - That point score is the score that's used for the match when calculating the archer's score for that competition rule.
   - When  Aggregation is "total"
      - The match's total score is the score for the archer in this competition round
   - Some participants will parttake in all matches, but most will not. When gathering scores, per participant, per rule, per round store the score. Do not create multiple records for the same paarticipant...

To calculate the result for a group:
- Add up the score for all scoring rules for all participants in that group
   - Keep track of the scores for the individual matches though, including information about which ones are not used in the result
- Order the results descending by that total score
- If two participants have the same total score, use as tie-breaker
   - the count over all matches in the competition of the number of times they hit an arrow with an X key-id
   - then, the count over all matches in the competition of the number of times they hit an arrow with a 10 key id
   - then, the count over all matches in the competition of the number of times they hit an arrow with a 9 key id
   - and so on until 1.
 - If even the number of 1s is identical, the archers are in the same place in the competition.
 - Assign position numbers using the same algorithm we use for UC17 - Live scoring

The results can be shown by pressing the "Results" button, which will navigate to a **printable** page in a new tab
  - Results have a header with the current date, ideally on each printed page
  - Result-rows show the position and the name of the participant and an asterix when participants truly are in the same place
  - Result-rows show all scores for all participants (with an entry in the participants for the tenant) for all rounds
  - Result-rows show the total score for each element in the scoring rules per participant
  - Results are grouped by the categories defined, the groups are presented sorted alphabetically
  - Results are ordered per group as defined
    - disqualified participants are put in a list-position as defined before; their total score is shown as 'n/a'
    - disqualified participants do not have a position number, it renders as '-'

#### UC14 - Profile management

As a loged-in user I want to be able to change my profile details such as my real name, my e-mail address and my password. 

I do this by selecting my username from the top menu, this acts like a button taking me to my profile page where I can change these data.

This screen is split into two sections, each with their own save button. One for the details like real name and e-mail. The other for changing the password. To be able to do that you must provide the user's current password. There is no API endpoint for that, so make sure that's added.

This function must also be available on mobile, where the user's name.

#### UC15 - Tenant administration

As a tenant administrator for the tenant that I am logged into, from the tenant's home screen I can select a function to manage sub-tenants for my current tenant.

When selecting this function, I navigate to a page showing me all sub-tenants of the current tenant. Clicking a sub-tenant from that list will show me an editor for the details for that sub-tenant being the name and logo image.I can see the current image or upload a new one. This should be a small image and can either be a SVG or a PNG. The preferred aspect ratio is 1:1 and the file size should not exceed 256KB.

The changes will be final when I save them.

Note: When for the tenant I am logged into a logo image is available then this image is shown in the header instead of the made-up avatar.

I also have an option on this screen for deleting the tenant and all its data, that has a very clear confirmation dialog telling me that this is final and not recoverable.

#### UC16 - Account management

Pressing the accounts button on the overview will take me to a page where I can see a list of accounts that are currently created inside this tenant. 
I can only access this page when I am a tenant administrator for this tenant.
For each account I list the account name, the real name, and the authorization level in this tenant.
Selecting an account from the list allows me to edit its details. I get an editor viiew for the real name, e-mail address, password, and account authorizations.
I can change these values and save.
I cannot change my own authorizations.
Only when the password field is non-empty it's used to set a new password for the user.
The page has the regular navigation for returning to the list or from the list to overview.

At the top of the list, consistent with the other lists in the system there is an Add Account button, which asks for an account name and then creates the unprivileged account opening the account editor on that single account. If creating the account fails, show an error and keep the add-screen open.

Note that errors from the UI should be coded, and the code should determine which text is shown so the UI can display an appropriate error message. This should also be done for login and documented in our memory both on this side as well as on the API side.


#### UC17 - Live scoring

Note: Two special endpoints, free of authorization, exists on the backend:
GET /live-scoring/match/{tenant-id}/{scope} that returns a list of all active matches for that scope, for each match the match ID, the date and the name are returned.
GET /live-scoring/match/{tenant-id}/{scope}/{match-id} that returns the live-score data for the match given the scope.

As a user (or narrowcasting system) from a web-browser I can navigate to the web-ui at the relative url `/narrowcast/{tenant-id}/{scope}` where `{tenant-id}` identifies the tenant and `{scope}` is a text-string.

When I do so, then the system will check if there are any currently live (active) matches that have rules for the specified scope.

*START* The system will use the `GET /live-scoring/match/{tenant-id}/{scope}` endpoint to load the list of active matches.

- If there are no matches then the system will display a simple message on a white background "There are currently no active sessions. Will check again in {time remaining} seconds." where {time remaining} starts at 30. When the timer reaches zero, will go to *START*, check again etc..
- If there are one or more active matches the system will display a result page for each of those, one at a time, switching to the next one when the current page times out. 
- The `GET /live-scoring/match/{tenant-id}/{scope}/{match-id}` endpoint will return how long the page should be displayed as well as a list of results to display.
   - When the end of the list is reached, the system will go to *START*, re-load the list, display, and so on. 

The result-page rendering:
- A separate css class will be used for the .live-scoring page.
- The result-page will be displayed full screen inside the browser window, it will always without scroll bars and will not be scrollable.
- The page will have a white background.
- At the bottom of the page there will be a "progress bar" of very limited height showing how much of the current page timeout has passed (no text)
- At the top of the page there is a full width header, vertically filling exactly about 6% of the available vertical space.
   - The header shows on the left the tenant logo and name
   - The header shows on the right the match date
   - The header shows in the middle the match's name 
- When in landscape mode, the page will have 3 columns. When in portrait mode the page will have 2 columns.
   - The fill-direction of the page is to first fill a column entirely then move to the next column
   - A column will never end with a block header
   - The page will be filled block by block. Each block is built as follows:
      - The name of the block
      - The entries are shown in multiple columns:
         - Leftmost in a fixed-width column the entry position
            - If the entry is marked as as `.needsTieBreaker` then the position number is followed by a *
         - Then one, two or three lines of data; filling all available space horizontally
            - Line 1 is rendered bold and shows `.line1`
            - Line 2 is rendered in a smaller font, non-bold showing `.line2` or not shown then .line2 is empty.
            - Line 3 is rendered in a smaller font, non-bold showing `.line3` or not shown then .line3 is empty.
         - Then in a smaller font the `.average` - but only if not `null`
         - Finally the `.score` value.
     - A column must never end with a header.
     - Headers with no entries are not rendered.
   - Font sizes should be adjusted to the screen size so generally 50 entries with typiclaly 2 lines divided over 10 groups should fit on the screen.
      - This means on portrait per column roughly 25 entries with 2 lines and 5 group headers and on landscape roughly 33 entries and 3 headers per column (so based on those numbers calculate font size, line height and block height for the entries, headers with whitespace)
 - A data model sample for the score endpoint therefore is:
   ```JSON
   {
      "timeout": "15",
      "logo": "(the logo-image)",
      "tenant": "Name of the Tenant",
      "matchName": "Name of the Match",
      "matchDate": "2027-09-01",
      "blocks": [
         {
            "name": "Recurve, Dames, Klasse A",
            "entries": {
               {
                  "position": 1,
                  "needsTieBreaker": false,
                  "line1": "Pieter-Bas IJdens",
                  "line2": "270,187 | 8x10, 7x9",
                  "average": 9.12,
                  "arrows": 45,
                  "score": 457
               },
               {
                  "position": 2,
                  "needsTieBreaker": true,
                  "line1": "Bob IJdens",
                  "line2": "270 180 | 9x10, 5x9",
                  "line3": "Personal best",
                  "average": 9.0,
                  "arrows": 45,
                  "score": 400
               }
            }
         }
      ]
   }
   ```

The `/live-scoring/match/{tenant-id}/{scope}/{match-id}` endpoint should be built in such a way that it follows the rules defined for that live score template. The algorithm to get to the grouped entries should be:
- For every participant, calculate their total score and their per-arrow average. 
- Check the scope for which group categories to use, and create one group for each n-tupe of categories. The name of the group is the concatenation of category values, in the order the categories are put in the match metadata.
- For each group, sort the scores according to the scoring rules for the match.
   - Assign position numbers starting at 1 to the sorted results. We're always sorting on score and count descendingly.
   - Do not pre-calculate all tie-breakers, instead only calculate a tie-breakers when you actually need them. 
     When using a tie-breaker "remember it", so it can be added to the line2 output if requested in the scope configuration.
   - When after applying all scoring rules archers are still at the same position in the sort, they will receive the same position in the results, BUT, needsTieBreaker will bet set to true.
     In that case the next number is skipped, so if 3 people are in position 1, then the next position is position 4.
- When creating the group entries:
   - Set position
   - Set needsTieBreaker
   - Set line1 to the full name
   - If "Include group score line" is set line 2 to the comma-space-separated concatenation of the group running totals
   - If "Include equalizer line" is set add to line2 the equalizers used, so e.g. "(457 + 9x10)
   - If include "Include personal best" line is set, then add to line 3 the static text "personal best is not supported yet" -- this is a feature we will add soon but not now.

This way we should end up with a good looking results page that we can lightweight narrow-cast to the smart TV screens that we have for displaying the live scores.

### Advanced use cases, future work

#### UCA1

To the participants list, when editing a member add to the view a list with all matches that the participant parttook in, including their score and average for that match.
