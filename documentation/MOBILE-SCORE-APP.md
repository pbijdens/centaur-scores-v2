# Scores application for entering scores from mobile devices

# General rules and guidelines

- This describes functionality that's entirely separate from the '../centaur-scores-web-ui' app, but does use the API implemented  in '../centaur-scores-api-v2'
- This web-app should be a separate, clean svelte app with minimal styling in the folder ../centaur-scores-mobile-web-scoring
    - The svelte app should have a svelte file per view and not be a single App.svelte file
    - The svelte app must use scss for styling
    - The svelte app must use typescript
- This web-app is intended to run on mobile devices in either landscape or portrait mode; it should support a variety of screen sizes and be highly adaptive as to how to render on different sized screens
- This web-app will have desktop compatible views, but for any choices made mobile is the first and foremost platform
- This web-app eventually wants to be deployed in a virtual folder behind a proxy, so e.g. with a mapping from https://public-address.example.com/scores/ to the application's local URL of http://localhost:9999/ . That deployment scenario must be supported and routing should definitely not break on this.
- This module exclusively uses the APIs from [PUBLIC-API-DESIGN.md](./PUBLIC-API-DESIGN.md) and *no other APIs* 
- This module is designed to receive the API base URL (the part up to and including the device ID) as input on its first call; aftewards it should remember this until it receives a new one; The first request that will start the application will be be a GET request to /?api=https://api.example.com/scorekeeper/{some-tenant-guid}/{some-match-guid}/{some-device-guid}&language=NL (URI encodd)
- This module should expect intermittent failures in network communication and should therefore keep track of updates to be sent in local storage, and retry them until there is a definite conclusion (4xx or 2xx)
    - These retries must happen on intervals in the background. Upon conflicts the application can and should notify the user and request a resolution.
- The application should ensure text on the screen is readable for a 50+ audience and whould refrain from using any fonts that are too small, unless accompanied by (or replaced by) easily visible icons
- The application uses local storage to store local state, and when resumed on the same tenant or refreshed will use the state from local storage to display exacttly what was shown before the user left
- Every 60 seconds, the application will use the `GET /scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}` endpoint to check if there are any server-side changes that are worth knowing about. If there are it will make the minimal changes needed to its internal data model, preventing screen or control refreshes when they are not needed.
- There is no complex navigation in this application and there is no need to use URL routing
- Back navigation should be fudged to return the user to the logical parent page
- The application is available in Dutch (NL) and English (EN), the language is obtained from the start-up URI and if not set will default to NL (Dutch)

# UI bascs

- The screens always hae a sticky header containing
    - The name of the match (black on white) prefixed by a Home icon (fills all remaining space)
        - Tapping here will take the user to the `Home Screen`
    - A language selection button (flag) (fixed size)
        - Drops down to a selection menu when pressed for switching languages
    - The synchronization status button (fixed size)
        - This shows on a dark-green background a white Thumbs up when there is no unsynchronized data.
        - This shows on a dark-orange background a white WIFI icon a when there is unsynchronized data.
        - This shows on a pure-red background an error-icon a when there is unsynchronized data and synchronization has already failed one or more times.
        - Tapping the synchronization button when there is any sunsynchronized data will trun it Yellow showing horizontal ellipses and will immediately retry sync, returning to one of the above states immediately afterwards.
    - When the `Score Card` view is active, the sticky header has a second line showing the name of the participant and the total score for that participant (imediately updated for any change made to the score card)

# Flow and screens

- The "No Active Match" view is shown when the backend's `GET /scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}` reports a 409 or 404 error. The page has a retry button that will try again, but no other navigation is possible. When (eventually) `GET /scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}` succeeds with a 2xx response, jumps to the application's `Home Screen` *that has no back navigation inside the app*
- 
- The `Home Screen` shows per current participant, in the order the participants are defined, in a scrollable list large tiles containing the name and number of the participant, the current total score of the participant (Score), the count of non-null arrows (Arrows shot) for the participant and the split-scores when configured (Split 1, ...). 
    - The app will need to calculate the total score of the participant by mapping the key ids in the arrow results to the point values in the keyboard keys for those and adding those up. If split scoring is defined, then calculate a split-total for every `groupEnds`, so if a match has 20 ends, the end scores are all 27 and the groupEnds value is 10, the split scores are 270 and 270, and the total is 540.
    - The `Home Screen` has no parent
    - At the end of the tile list there will be an "empty tile" just containing a big plus icon. The tile is the same size as the other tiles, pressing this  will open the `Add Participant` view. This tile can't be swiped. If `allowModifyParticipants` is false this tile will not be present.
    - If `allowModifyParticipants` is true, swiping a participant tile to the left will reveal two buttons
        - Button 1 is an Edit button 
            - Only available when the participant does not have a `tenantParticipantId`
            - Only available when the match's `allowCustomParticipants` is true
            - Will open the `Edit Participant` view
        - Button 2 is a Remove button
            - Tapping this will push the removed participant data to the server using the `PUT /scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}/participants` endpoint. If that rejects the change, an alert is shown that the remove was not possible; the delete request is also locally ignored in that case. If it succeeds trigger sync and wait for the result.
    - Tapping the tile will take the user to the `Score Card` view for the user.

- The `Add Participant` view
    - Has as parent the `Home Screen`
    - Shows a search box, with below it a filtered list of potential participants as returned by the `GET /scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}/participant-options` endpoint. These are divided in three labeled catgories by separator lines containing the text "Unassigned", "Available", "Already assigned" (empty categories are not shown).
        - Typing in the search box will filter the participants list to only show participants where the types value is a substring of either the name, the info string o the federation number.
        - A participant in the list is rendered such that its name is featured on line 1, smaller on line 2 on the left will be the info line containing the concatenated categories info string (left aligned). On the right will be the federation number, if present (right aligned)
        - Selecting a participant from the list will 
            - Push the updated participant data to the server using the `PUT /scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}/participants` endpoint. If that rejects the change, an alert is shown that the action failed, and nothing will have changed. Keep the screen open until the user cancels the action by pressing the home button or back navigation on the device.
            - On success, close the `Add Participant` view refresh the match-data then return to the `Home Screen`
    - If the match allows this (`allowCustomParticipants` is true) show below the list "Add unlisted participant"
        - Pressing that will show an edit screen for the participant:
            - Input for the optional federation ID
            - Input for the required full name
            - Single-select options for the values of each category, in the order returned from the API
            - Save button at the bottom, only available when all categories are chosen and a name is entered           - 
            - Cancel button that will clear the fields and return to the List view
            - Pressing save:
                - Push the updated match-participant data to the server using the `PUT /scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}/participants` endpoint. If that rejects the change, an alert is shown that the action failed, and nothing will have changed. Keep the screen open until the user cancels the action by pressing the home button or back navigation on the device.
                - On success, close the `Add Participant` view refresh the match-data then return to the `Home Screen`

- The "Edit Participant" view
    - Will show an edit screen for the participant:
        - Input for the optional federation ID (pre-populated with the current value)
        - Input for the required full name (pre-populated with the current value)
        - Single-select options for the values of each category, in the order returned from the API (pre-populated with the current value)
        - Save button at the bottom, only available when all categories are chosen and a name is entered           - 
        - Cancel button that will clear the fields and return to the `Home Screen`
        - Pressing save:
            - Push the updated match-participant data to the server using the `PUT /scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}/participants` endpoint. If that rejects the change, an alert is shown that the action failed, and nothing will have changed. Keep the screen open until the user cancels the action by pressing the home button or back navigation on the device.
            - On success, close the `Edit Participant` view refresh the match-data then return to the `Home Screen`

- The `Score Card` view
    - Remember this has an additional line in the sticky header that must be updated when scores change
    - A score card normally is a sheet of paper that per participant has 1 line on it per end with on that line the end number, a number of fields for writing the arrow scores in, a field for writing down the end total and a field for writing down the running total
    - In this app we're going to mimic this, but we're going to take into account mobile usability and screen sizes:
        - Instead of a single line, we're stating that a score card has a block per end, where ideally the block is on a single line but when screen sizes do not allow it may wrap into multiple lines.
        - The block per end shows 
            - first, obviously read-only the number of the end
            - next, obviously touchable one 'button' per arrow showing the arrow key's label (not the ID), highlighting the value by the arrow key's color, and rendering `null` value fields as '-' with a lightgray highlight.
            - next, obviously read-only the sum of the scores for these arrows
            - next, obviously read-only the sum of this end and the previous ends in the match
            - next, obviously read-only the running total of the current group (in parenthesis) that this end belongs to
        - Touching an arrow button
            - When the user touches ANY arrow button with a null-score to open the `inline keyboard panel`, the system will initially react as if the first null arrow in that end was pressed. If the keyboard is already open on the row, touching any arrow score field will give that field focus, regardless of whether it's null or not.
            - Will open an  `inline keyboard panel` row below the end's box
            - Will scroll the bottom of the  `inline keyboard panel` into view even when that pushes the end-box out of view partially that's not a problem, the user can scroll back up
            - Will highlight the touched arrow as "active" or having focus
          - About the `inline keyboard panel`
            - This shows in order all the keys in their specified color (with a readable foreground color) from the keyboard definition; reducing to only those keys that are in `availableKeyIDs` for the participant according to the API
            - One 'delete' key using the universal backspace icon is added to every kayboard always; this will set the values for the arrow to null again.
            - One 'hide' key using whatever is the most appropriate icon for hiding the keyboard
            - Key sizes should be optimized for touch
            - In case there is any confusion still, while using the `Score Card` view, the system keyboard must never be shown; regular input controls are not used in this view.
            - Pressing any other key will:
                - Register the score update in the local data for this participant so it gets pushed on the next sync, making sure old is set to whatever was in the key's field before the key was pressed.
                - Put focus on the next null arrow score in the same end
                - If there is no next null arrow score in the same end, keep focus on the current arrow and hide the keyboard
    - Swiping left or right will take the user to the next or previous score card
    - Ends will be visually separated in a subtle manner

- 