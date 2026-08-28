# Personal Best Score

Unlike most other feature, except the accounts, this feature partly works across tenants. The feature can be enabled for a specific tenant, and the configuration and data is then inherited by all sub-tenants. It is not possible to re-configure or manage this at sub-tenant level.

## Requirements (descriptive)

The basic idea is that this system works with personal best data that it does not exclusively own. The way that *archers* and *match classifiers* (the types of matches) are identified is determined externally and passed to this system through configuration, the list of personal best scores for an archer is uploaded from an external system. The CentaurScores system will keep track of its own updates to that list (which score was improved upon by which archer when), and will be able to regularly export a list of results per archer. The CentaurScores system will internally keep a log of all recorded improvements to the personal best score, and only the improvements.

As an Administrator, on the Overview tab on the Homepage of the management Web UI, there is a button I can press called "Personal Best" that takes the user to the personal best control page. This button is only available on tetants on which the personal best feature is either not enabled yet, or is enabled on the tenant itself. Sub-tenants inheriting an enabled personal best environment will not show this button.  Take into account that the feature is implicitly enabled if it's enabled on any super-tenant of the active tenant (in which case this button is not shown).

A personal best log is a table at the level of the tenant that enabled the feature, in which are rows with the following information:
- Date the recorded score was achieved
- Federation number
- Name
- Discipline
- Match classifier
- Score
- Update date

In this tables you will for one archer ONLY find lines logged when they improve upon the previous entry for that same archer for that same match classifier given the same discipline. So per discipline you can get the user's personal best by reading the entries with the highest date. If there are multiple entreies with the highest date for the same archer/discipline then the one woith the highest score is the personal best.

## Use cases

### UC1 - Enable personal best tracking at tenant level

As an Administrator, on the Overview tab on the Homepage of the management Web UI, there is a button I can press called "Personal Best" that takes me to the personal best control page. 

Assuming the feature is not enabled yet on this tenant, nor on any super-tenant of this tenant:
- I can see that the feature is not enabled yet because the page shows me a message "Personal-best tracking is not yet enabled for this tenant. You can enable it from this page's menu."
- To enable the feature I use the master control switch "Enable personal best tracking". This is in the page-level DropdownMenu.

If personal best tracking would already be enabled, I would see this on the page because it would show me the personal best management page..

After enabling the feature, the page will reload its data and show the personal best management page.

### UC2 - Disable personal best tracking at tenant level

As an Administrator, on the Overview tab on the Homepage of the management Web UI, there is a button I can press called "Personal Best" that takes me to the personal best control page. 

Assuming the feature is enabled on this tenant, not on any super-tenant of this tenant:
- To enable the feature I use the master control switch "Disable personal best tracking". This is in the page-level DropdownMenu. Selecting this function will show me a conirmation request asking me if I am sure that I want to disable te feature. The feature can be re-enabled later, but any personal best improvements while the feature is disabled will not be captured by this software.

After disabling the feature, the page will reload its data and show the message that the feature is not enabled yet.

### UC3 - Configuring match classifiers

As an Manager, on the on the personal best control page there is a button called "Configuration" that takes a user to the configuration page. This is in the page's Dropdown Menu.

On the configraton page, in a box "Match classifiers" of "Wedstrijdtypes" (nl) with its own Save buttom I can configure the match classifiers. A classifier is no more than a string that uniquely identifies a type of match. The existign classifiers are rendered as a list of strings, shown sorted with for each string on the far right a delete icon. Clicking any item will allow me to edit it. Only when I press "Save classifiers" the list is persisted.

### UC4 - Configuring disciplines

As an Manager, on the on the personal best control page there is a button called "Configuration" that takes a user to the configuration page. This is in the page's Dropdown Menu.

On the configraton page, in a box "Disciplines" the available disciplines for personal best tracking can be managed. Also these are no more than a list of string values, except that to each string value one can attach one or more values from this tenant's and all sub-tenant's categories (the same value can not be attached to multiple disciplines in this list). The selected values will be rendered in the read-only bullet-list as "{tenant name}/{category name}/{value name}". Clicking the list item, will open an editor that shows the name of the discipline itself with below it a list of all nodes for all categories for this tenant and all sub-tenants, ordered by tenant name, then category name, then value name. Each value has a checkbox indicating if it is selected. If a value is already selected for another discipline, the checkbox will be off, disabled and grayed out. Pressing "Save disciplines" on this box will persist all the changes to the tenant.

We now have configured the list of discliplines used in the external personal best score registration as well as a mapping to the tenants' discipline configuration. 

> This system does not attach any special meaning to the categories defined at tenant level, nor to how these are mapped at match level. Managers are entirely free to decide how they wil group participants. However, to properly set up a link to personal best scores, it's necessary to define this mapping here once.

### UC5 - Export configuration

As an Manager, on the on the personal best control page there is a button called "Configuration" that takes a user to the configuration page. This is in the page's Dropdown Menu.

On the configraton page, in a box "Disciplines" in a box "Export configuration" the user can define how the personal best data from the system is exported. 

First there is an option for what to export, being:
- All updates for all participants all times
- Changes after the last import

The name of the data table that's exported, defaults to "PersonalBestExport"

Next there are column mappings. This is a table with rows that can be re-ordered or deleted by the user, or a new one can be added. Solumns in this table are:
- Column name:
  The name the coluumn will have in the export, text field, may not be empty
- Field:
  A dropdown with the following options:
  - Federation number
  - Full name (full name of the first listed Participant record that's used in the export, if there are only unlisted participants for this bondsnummer, the first of those)
  - Lastname (last name of the first listed Participant record that's used in the export, if there are only unlisted participants for this bondsnummer, the first of those)
  - Date (the user can pick a date format also, either YYYY-MM-DD, DD-MM-YYYY or MM-DD-YYYY)
  - Discipline (mapped back from category to the above mapping defined)
  - Match classifier (from the match)
  - Score (total score for the participant from the match)
  - Export date (either YYYY-MM-DD, DD-MM-YYYY or MM-DD-YYYY)

The default configuration is:
|Column name|Field|
|-----------|-----|
|Datum|Date as YYYY-MM-DD|
|Bondsnummer|Federation nummer|
|Naam|Full name|
|Discipline|Discipline|
|Wedstrijd|Match classifier|
|Score|Score|
|Toegevoegd|Export date as YYYY-MM-DD|

Pressing the "Save export configuration" button persists the export configuration.

### UC6 - Import configuration

As an Manager, on the on the personal best control page there is a button called "Configuration" that takes a user to the configuration page. This is in the page's Dropdown Menu.

On the configraton page, in a box "Import configuration" I can configure the following:

- Import table name - the name of the table in the Excel document that contains the data to be imported. Defaults to "Resultaten"
- 
- Mappings: Per information item which column this should be read from:
    - Date (default: Datum)
    - Federation number (default: Bondsnummer)
    - Name (default: Naam)
    - Discipline (default: Discipline)
    - Match classification (default: Wedstrijd)
    - Score (default: Score)
    - Update date (default: Toegevoegd)

When being offered a worksheet for import, the system will use this configuration to extract all information that will be imported.
 
### UC7 - Uploading authoritative personal best scores

When I want to upload a new authoritative list of personal best scores, I can do so directly from the personal best control page using a tile-button "Import list".

When I press the button, I am requested to upload a file and start Import.

The system will, as a consequence, read the configured import table from the excel sheet that was uploaded, and will process it row by row.

During import, the system will not need to look at any participant list. It will simply do the following per row:
- Find all entries in the personal best log (that is kept in the tenant at which level the support is built) for the 'Federation number', 'Match classifier' and 'Discipline'
- If there are none, add the record
- If there are entries but already one exists for this date and score, ignore the row, it's already present
- If there are entries AND the Score for the last entry with the (highest or equal) date on or before the proposed log entry's Date is lower (not equal, strict lower)
  OR there are entries BUT none on or before the proposed log entry's Date
    - THEN 
        - Add the entry to the log, 
        - And delete all entries with a date higher than the entry's date and a lower or equal score (for the same archer, discipline and match classifier)
    - OTHERWISE
        - Add the entry to the conflict list (as "related import record), together with all entries before that with a higher score (as offending records), as conflict type "I cannot insert this item because there already is a higher registered score at that time."
 
If at the end of the process for an archer/match classifier/discipline there are still records in our local log with a higher score than the highest score we imported for that archer in thsi run *AND* there were more than 0 scores offered for import for that archer
    - Add all entries with scores higher than the highest inserted score to the 'conflict list' (as offending records), with no "related import records" and as type "Archer has higher scores than imported personal record"
    - After Import finishes, provide the user with a list of all archers that we have a conflict for and ask if we should do:
        - Delete our offending record(s), keep only imported record(s) [true authoritative]
        - Ignore imported record(s) [accept our recrds as a source of truth also]

When import is done report on what was imported (so many new archers, so many new registrations).

### UC8 - Exporting personal best scores

When I want to upload a new authoritative list of personal best scores, I can do so directly from the personal best control page using a tile-button "Export personal best updates".

This creates an excel export file {tenantname} personal best updated.xlsx containing what was requested.

### UC9 - View personal best scores

As a manager I want to view the personal best scores of archers. For this I use the "View personal bests" tile button on the personal best control page.

This opens a screen where I get a filtered list of all registered archers with columns "Federation number", "Name", "Discipline", "Classifier", "Date", "Score", with per column a filter-option to filter the list.

The list is sorted by Name, then federation number, then Discipline, then Classifier.

The list contains only the most recent (highest) personal best log entry per archer/discipline/classifier

### UC10 - Register personal best automatically

As soon as a match is deactivated, the system will 
1. check to see if personal bests are active for the tenant, if not, done.
2. check to see if the match defines a Match classifier for personal best use in its metadata (add it to the data model, to the matchj template and to the matc editor), if not ignore
3. check each participant
    1. must be on a participant list 
    2. must have a federaion number
    3. check to see if *at the registered match date* for the match using the mappign defined for discipline and for the classifier at the defining tenant level there is a log entry relevant with a lower score than this one, if so add this record.

### During a match

If enabled in the scope for live scoring, and personal best is enabled. If the current match looks (based on arrow average and arrows shot) like the archer will beat their personal best, highlight the line. Check server side, render client-side. Also for all archers for whom we have apersonal best server-side include that personal best in the result line; render it client side when enabled.

## Questions

### Is the federation number globally unique, or only unique within the owning tenant?

It's unique within the  tenant that defines the personal best list; between unrelated tenants these may differ for the same archer even.

### Should a personal-best log retain the original name from each imported/automatic record, or should names be refreshed from participant data?

Keep a single alongside the logs for a bondnummer. Any time we import we can refresh by loading a new name from the import. If we register a personal best for a currently unlisted archer, we can use the name that is registered in the match we're registering the scores for. That maked more sense than what I mentioned before.

So, when I mentioned:
  - Full name (full name of the first listed Participant record that's used in the export, if there are only unlisted participants for this bondsnummer, the first of those)
  - Lastname (last name of the first listed Participant record that's used in the export, if there are only unlisted participants for this bondsnummer, the first of those)

I was wrong. That is not needed.


### What exactly constitutes an “import” watermark for exports: upload completion time, source update date, or individual imported rows?

If we register with a log entry it's imported, then any entry after the last imported for archer/discipline/classificatin is NEW (or any entry if there is no imported entry).

### When disabling tracking, should existing personal-best data remain visible and exportable?

In the system: Yes, Exportable: No, Visible: No. When disabled, the request for PR information in the scope settins must be ignored.

### How are duplicate classifier names and discipline names handled?

Forbidden. Must be prevented at all cost. Do not allow save and mark as validation error.

### What does “arrow average and arrows shot” mean operationally for live-score prediction, and what threshold marks a likely improvement?

Arrow average for an archer during a match is the total score of arrows shot divided by the number of arrows shot (i.e. non-null) for that archer.

The personal best score has an arrow average of that score divided by (the number of ends * the number of arrows per end) so if the match is for 3*10=30 arrows and the personal best is 240 then the arrow average for that is 8.00; if the archer is currently on 121 after 15 arrows, then the arrow average is 8.07 which suggets the archer is crrently above thier personal best score. So if personal best is enabled, we can in the results on the PB line show: PB: {archer\'s personal best to 2 decimals}. and if the per-arrow average they currently are achieving is higher we can highlight that text.

### Should import rows with missing or invalid mappings be rejected, skipped, or added to the conflict list?

Not sure if that would be a conflict per se, but these rows should be skipped and rows skipped because they contain invalid data should be reported on, but no resolution should be offered. Let the user fix the input and import again, that should be safe.

### Does “new archers” mean distinct federation numbers in the import, or federation numbers not previously present in the local log?

Yes new archers meansin this context federation numbers we never saw before.

### Decide whether disabling a feature preserves its historical log and configuration. The document implies that it does.

Data is preserved on disable.

### Also important

After a match is deactivated, and when personal bests are enabled for the tenant and when the match specifies a valid classifier and the archer has a supported discipline, if the archer is not yet in the personal best log for the tenant, add them.