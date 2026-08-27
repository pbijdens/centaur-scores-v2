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
  - a live score device will query the special endpoints at `http://internal-score-api-url:port/live-scoring/match/{scope}` and `http://internal-score-api-url:port/live-scoring/match/{scope}/{match-id}`
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

- The backend API offers read-only unauthenticated endpoints providing the data needed for live score display via `http://internal-score-api-url:port/live-scoring/match/{scope}` and `http://internal-score-api-url:port/live-scoring/match/{scope}/{match-id}`.
  - The first endpoint lists open matches, across all tenants, configured for the scope; the second returns one match's live-scoring page (its tenant resolved from the match itself, not from the URL).

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

## Application-specific design documents

- [Management UI Design](../centaur-scores-web-ui/DESIGN.md)
- [Mobile Score App Design](../TBD.md)
