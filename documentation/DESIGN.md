# Centaur scores

## Overview

Centaur scores is a set of applications that together helps organize archery matches by offering the following services:
- A very **mobile-friendly** web-score-application that's used by archers to enter their scores during the match
  - backed by .NET Web API backend that's used by the client to store and retrieve match-related data
- A mobile friendly web interface for managing the system
  - backed by .NET Web API backend that's used by the client to store and retrieve match-related data

## Data store

All data is stored in a MySql database
All column names, table names etc. must be lowercase making it possible to exchange dumps between case sensitive and case insensitive mysql deployments

# Model

## Tenants

- tenants have a name, a logo, a list of admin accounts and a list of match-management accounts
- tenants may define one or more lists of (potential) participants 
  - the list has a name and a toggle that can be used to deactivate the list (e.g. if is was only temporarily needed); lists can't be deleted once any match uses them.
  - on the list  each participant has
    - a system-internal unique participant id
    - a last name (for ordering purposes)
    - a full name
    - a federation-assigned number
    - a single value for each of the categories
- tenants may define one or more categories with
  - a flat list of values each represented by a single unique name and a system-internal category-unique identifier, modeled as an integer
  - a system-internal unique category id
- tenants have a parent tenant

- there is one special "Root Tenant" that is created during system initialization and by default has a single account 'centaurscores' 
  with a password 'centaurscores' with all authorizations.

### Accounts

- accounts belong to a single tenant
- accounts have a username, a password and optionally a display name and optionally an an e-mail address
- accounts get assigned a system-wide unique ID
- accounts have authorization profiles
  1. tenant administrator, which allows for all operations on the tenant including creating sub-tenants and deleting sub-tenants
     1. authorization is inherited for all sub-tenants of this tenant
     2. tenant delete is never allowed for the highest level tenant a logged-in user is administrator for
     3. implies also all authorizations listed below
  2. ability to view accounts and manage all tenant-level data including matches, and competitions
     1. authorization is inherited for all sub-tenants of this tenant
     2. implies also all authorizations listed below
  3. ability to view all data for the tenant
     1. implies also all authorizations listed below
     2. note: view-only accounts may be assigned additional rights for specific matches and competitions

### Match templates

- At tenant level it's possible to manage a collection of match templates that can be used when creating matches, these allow pre-defining all match configuration data except the actual participants and scores

## Matches

- a match has a unique identifier
- a match has a name and a date
- a match has a short code that's included only when exporting match results for processing in external systems
- a match belongs to a single tenant
- a match refers to a set of tenant-level categories that are relevant
- a match defines a number of ends with a number of arrows per end plus optionally by how many ends scores should be grouped
  - grouping scores is for result-visualization only; used for for example 60-arrow matches to show a total score per 30-arrow group.
- optionally matches have a source-list of participants
- matches have a list of participants with for each participant
  - a per-match unique ID for the participant
  - either "a system-internal unique participant id" that should come from the source-list of participants when defined
  - or if the option for free-participant entry is enabled in case an unlisted member is added
    - a name
    - a federation-assigned number (optional)
    - one value for each of the categories that have been marked as relevant to the match
- matches have a default entry-keyboard for scores
  - per 'key' has a label, a short ID, a color {Yellow, Red, Blue, Black, White} and a numeric score value
    - examples: 
      - label = 'X', id = 'X', color = 'Yellow', score = 10
      - label = '10', id = '10', color = 'Yellow', score = 10
      - label = '9', id = '9', color = 'Yellow', score = 9
      - label = '8', id = '8', color = 'Red', score = 8
      - label = '7', id = '7', color = 'Red', score = 7
      - label = '6', id = '6', color = 'Blue', score = 6
      - label = '5', id = '5', color = 'Blue', score = 5
      - label = 'Mis', id = 'M', color = 'White', score = 0
      - label = 'Del', id = null, color = null, score = null (special case, wipes the input)
- per category can define which key IDs are not available, e.g.
  - Compound: ['5', '4', '3', '2', '1']
- matches have per participant a list of all entered single-arrow scores
- matches have per participant the score values for all groups, plus the total score that corresponds to the last stored 
- matches have a list of score-entry devices where each device
  - is assigned a random 128-bits guid unique identifier 
    - background: for a match we'll  print a page with on it all these QR codes that represent the base API URL for the device to use, e.g. 'http://internal-score-api-url:port/scorekeeper/{tenantguid}/{matchguid}/{this guid}'
  - is assigned a name (e.g. Baan 1)
  - May either 
    - be restricted to specific participants (in a specific order) only; participants must be added from the source list
    - or may allow selecting the participants from the source list in which case participants may be added to the match
    - or may allow selecting the participants from the match's participant list
    - or may be configured to either select from the source list or allow adding participants manually outside what the source list defines
      - in that case, to be added as a new participant, *all* categories for the match must be specified for those participants
  - A single participant to the match is managed by a single device only. When a participant is added to a device, they are removed from all other devices configured for the match.
- matches can either be open for score entry or closed
- per match define
  - one or more scoring rules and their order, choose from
    - total score
    - count of 'short ID' (that way we can count X-10s separate of regular 10s)
    - so a match could for example have as scoring rules:
      - total score, if equal count X's, if atill equal count 10s, if still equal count 9s, if still equal the archers are in the same position.
- matches can define live score scopes by adding one or more configuration rules with:
  - scope: "all" or "any url-safe identifier for the scope"
  - the grouped by categories that tell the system how to group the scores
  - what to show:
    - just position, name, scores
    - include the per-arrow average
    - include a line with "group scores" for the totals of grouped ends
    - include a line with equalizer values (when needed)
    - include a line with the current personal best as per-arrow average
  - a live score device will query a special endpoint for results at http://internal-score-api-url:port/live-scores/{tenant-id}/{scope} or http://internal-score-api-url:port/live-scores/{tenant-id}/all
  - a match has a list of accounts that can manage it in addition to those users who already implicitly have those rightd

## Competitions
  - competetions are managed per tenant
  - a competition has a unique id and a name and a date range (start date, end date) in which it's organized
  - a competition has a list of accounts that can manage it in addition to those users who already implicitly have those rightd
  - competitions have one or more rounds in a specific order
    - each round has a short name and a long name, e.g. '08-26', 'Round 1 18m3p'
    - each competition round can have one or more matches assigned to it; when more matches are assigned to one round, then the results of those matches are combined
  - the same match can be added to multiple competitions or multiple rounds per competition
  - only match participants with "a system-internal unique participant id" assigned are considered for competition results 
  - a competition has scoring configuration rules defined for example as follows
    - group by Discipline, Klasse
    - scores to add per archer:
      - Score 1: from the rounds [Round 1 18m3p, Round 2 18m3p, Round 3 18m3p, Round 4 18m3p, Round 5 18m3p, Round 6 18m3p, Round 7 18m3p] select per archer the highest 5 scores; when archers have less than 5 scores *disqualify* 
      - Score 2: from the rounds [Round 1 25m3p, Round 2 25m3p, Round 3 25m3p, Round 4 25m3p, Round 5 25m3p, Round 6 25m3p, Round 7 25m3p] select per archer the highest 5 scores; when archers have less than 5 scores *disqualify*
  - Another example
    - group by categories Discipline
    - scores to add per archer:
      - "Score 1": from the rounds [Round 1 18m3p, Round 2 18m3p, Round 3 18m3p, Round 4 18m3p, Round 5 18m3p, Round 6 18m3p, Round 7 18m3p] select per archer the highest 5 point totals; when archers have less than 4 scores *disqualify* 
      -  Score 2": from the rounds [Round 1 25m3p, Round 2 25m3p, Round 3 25m3p, Round 4 25m3p, Round 5 25m3p, Round 6 25m3p, Round 7 25m3p] select per archer the highest 5 point totals; when archers have less than 4 scores *disqualify*
      - (informational: point-totals are calculated by calculating all scores per round for the rouping, taking into account equalizer rules as defined in the match, and then assigning scores according to the F1 scoring system, i.e. the best gets 12, next 10, then 8, 7, 6, ... where evryone with a non-zero score gets 1 point)
  - So basically scoring rules consist of a list of group-by categories, and then one or more named scores that get added up to calculate a total score, so in our example the participant gets "Score 1", "Score 2" and "Total" where the latter is the sum of the other scores.

# Components

## Backend REST API

The API is JSON-based, and unless mentioned otherwise the endpoints need a valid logged in user (see below)

- The backend API offers read-only unauthenticated endpoints providing the data needed for live score display via the http://internal-score-api-url:port/live-scores/{tenant-id}/{scope} and http://internal-score-api-url:port/live-score-api/{tenant-id}/all endpoints
  - When multiple matches are open and configured, the endpoint includes the data for all those matches; it's up to the displaying system to decide how to handle that.

- The backend API offers unauthenticated endpoints for the score-keeping UI using the base path http://internal-score-api-url:port/scorekeeper/{tenantguid}/{matchguid}/{deviceguid}
  - There will be APIs for fetching the list of available participants (either via the source or from the match) with per participant an indication at which device that participant currently enters their scores
  - When participants have been added without being in a source list (provided the match configuration allows that) then they are also available for selection 

- There will be APIs for:
  - Authentication  
    - Login API will return a valid JWT token for the provided username/password
      - The secret for signing the JWT tokens is stored in the application configuration or the system environment
    - The JWT token will be medium-short-lived (4hrs, configurable at tenant level) and contain the tenant the user logged in for, the system wide unique user ID as well as the user's real name and e-mail address using appropriate claims.
    - All other endpoints that require authentication require a valid JWT token to be provided as a Bearer token (OAuth style)
  - Tenants (CRUD)
    - Update actions require tenant admin authorization on the tenant itself or on the parent tenants
    - Tenant configuration
      - Participant lists CRUD
        - Participant list member CRUD
      - Match templates CRUD
      - Account CRUD 
        - Assigning authorizations requires tenant administration authorization for this tenant or any of its parent tenants

  - Matches (CRUD)
    - Separate endpoints for managing match participants (assign based on system wide participant ID or if allowed CRUD match-scoped participant)
    - Separate endpoints for seeing / updating participant scores
    - Separate endpoints for managing match score devices
    - Separate endpoints for managing live scoring scopes
    - See results

  - Competitions (CRUD)
    - Separate endpoints for round management
    - Allow for (un)assigning matches to a round
    - Allow for defining and changing competiton scoring rules

  - Match Results
    - There will be an authenticated API for fetching the match results according to the match's scope settings as JSON

  - Competition Results
    - There will be an authenticated API for fetching the competition results as JSON

## Management UI

The management UI is a very simple front-end that offers the following functionality:


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

#### UC5 Category management

As a user, when Iam logged in and have sufficient rights, from the Tenant Home screen I can select a function to manage categories this takes me to the category list. That list is ordered alphabeticcally by default.
Opening any category allows me to Add/Remove values or even delete the category if it's not used.
Once an category is used by a match it can't be deleted anymore.

#### UC6 - User wants to set up a match template

As a user, from the Home screen I can select a function to manage match templates.

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
- Define per category rules for which keyboard elements are disabled
  - e.g. For the categorynode "Compound" from the "Discipline" category the value kys with ID '5', '4', '3', '2', and '1' are not available.
- Define the scoring rules for calculating the match result
- Define the live scoring scopes

#### UC7 - Managing lists of participants

As a user the participant list screen allows me to CRUD lists of participants. Participant lists have as metadata a name and an indication of whether they are still active.

Inactive participant lists are only shown in the full list we see when we click the function button or when we go via "Show all". The list is sorted so active lists are on the top, next by name.

Inactive participant lists are rendered visually different, but are clickable nonetheless, opening their details.

Participant list details consist of a metadata editor, and a list members editor. Users with sufficient rights can edit and save the metadata. The participants list shows all participants in the list, ordered by last name. Participant lists in context of matches always show all relevant category values also, where the relevant values are the ones identified in the match definition, in the order identified there. So we could have Pieter-Bas IJdens (Recurve/Klasse C) in the participant list when I need to select an archer.

Clicking (or touching) a participant shows a screen with:
- Their metadata as defined in the model, editable when the user has sufficient privileges
- The current value for each category, with the possibility to change it if the user has the rights

In order to save, all values should be filled in.

From the single participant editor it's easy to return to the participant list. Goiing to the tenant homepage is also trivial.

#### UC8 - Managing matches

Selecting that function, or selecting show all will take the user to the list of matches for the tenant.

The list shows for each match the name, date and number of participants currently assigned.

The list will be sorted such that the currently active matches are always on top, rendered visually different (active)

Next are all current and future matches not shown yet, ordered by date, ascending.

Next are all past matches; visually different ordered by date, ascending.

Above the list there is a toolbar with options to add a match and a filter to filter the list on "Only future matches" or "Only past matches". There is a text field that can be used to filter the list (on name)

Clicking or touching the match navigates to the matches homescreen. On this screen the user can, given sufficient privilege:
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

Alongside the filter options fort the list, there is an option to add a new match to the system.

There is a results dropdown button also in the list options, with a dropdown per result scope, taking the user to the results link we advertise for the match.

Allow assigning users as match admins

#### UC9 - Exporting matches

Matches can be exported to CSV, where each row contains:
federation number, full name (or last name if full name not set), (next, one category value per column), score group 1, ..., score group n, total, equalizer 1, equalizer 2, ...

Export is initiated via a button on the match homepage.

#### UC10 - Deleting a match

tenant admins can delete matches from the match homepage. This does require confirmation.

#### UC11 - Managing competitions

Selecting the function will take the user to the list of competitions.

The order of that list is such that the currently relevant competitions are at the top of the list, visually different from future competitions (shown next) and past competitions (shown last); when start dates overlap, sort order is secondary by name.

I don't see a reason yet to filter the list, but should for consistency allow filtering on past, active, future.

Selecting a competition takes the user to the competition homepage.

#### UC12 - Deleting a competition

tenant admins can delete competitions from the competitioon homepage. This does require confirmation.

#### UC13 - The competition homepage

Shows the list of rounds (manageable, add, delete)

Shows assigned matches per round.

Shows scoring rules in place (editable, manageable)

Allows
- Editing metadata and saving edits
- Deleting the competition for tenant admins only
- Assigning users as match admins
- Showing the results by navigating to a **printable** page in a new tab
  - Results have a header with the current date
  - Results show scores for all participants for all rounds
  - Results show the total score for each element in the scoring rules per participant
  - Results are groups by categories defined in the rules
  - Results are ordered per group on total score
    - disqualified participants have a 0 total score and tail the list
    - disqualified participants do not have a position number
    - participants with the same score share a position number

#### UC14 - Profile management

As a loged-in user I want to be able to change my profile details such as my real name, my e-mail address and my password. 

I do this by selecting my username from the top menu, this acts like a button taking me to my profile page where I can change these data.

This screen is split into two sections, each with their own save button. One for the details like real name and e-mail. The other for changing the password. To be able to do that you must provide the user's current password. There is no API endpoint for that, so make sure that's added.

This function must also be available on mobile, where the user's name.


## Centaur Scoring App

The Centaur Scoring App is a simple web application that uses the `centaur-scores-api-v2` software as a backend.

