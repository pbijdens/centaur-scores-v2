# Purpose

In this document we're going to define the public API design for the CentaurScores software

# General rules

- Note that if there is going to be considerable logic in an API controller, it should delegate its work to a service instead.
- All public API endpoints are free of authentication.
- We therefore need a log line per call that identifies the caller by IP and shows all parameters to the call.
- All public API endpoints are bound to a specific match and device and they all mnust fail with a "MATCH_NO_LONGER_ACTIVE" error code response and a 409 HTTP error when that match is not active.
- All public API endpoints that are used for scorekeeping devices are routed below `/scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}`

Endpoints needed:

## Get device-specific match information

`GET /scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}`

Returns a JSON structure of type `ScorekeeperMatch` with the following structure

|property|example|description|
|--------|-------|-----------|
|device|`"Baan 1"`|The name of the device from `ScoreDevice.Name` from `Match.Devices` |
|match|`"Ronde 12 18m3p"`|The name of the match from `Match.Name`|
|ends|`20`|The number of ends in this match|
|arrowsPerEnd|`3`|The number of arrows shot per end|
|groupEnds|`10`|The number of ends to group for split scores.|
|categories|`[{...}]`|Ordered array of relevant categories for the match with all possible values, elements are of type `ScoreKeeperCategory`|
|allowModifyParticipants|`true`|True when it's allowed to modify the list of participants on the device, false if the participant list is fixed.|
|allowCustomParticipants|`true`|True when it's allowed to add participants that are not in the tenant-level participant list that was configured for the match.|
|keyboard|`[{...}]`|Ordered array of the keyboard keys that may be used for score entry in this match, elements of type `ScorekeeperKey`|
|participants|`[{...}]`|Ordered array of 0 or more match participants each element of type `ScorekeeperMatchParticipant`|

Where the `ScoreKeeperCategory` objects have structure:
|property|example|description|
|--------|-------|-----------|
|id|`"4EDB0001-0100-0200-5234-70101710171A"`|ID of the category|
|name|`"Discipline"`|Name of the category|
|values|`["Recurve", "Compound", "Barebow", "Hout"]`|Ordered array of all possible values|

Where the `ScorekeeperKey` is as follows:
|property|example|description|
|--------|-------|-----------|
|id|`X`|ID the key is referred by in all communications and in the score registration|
|label|`X`|The label to display on buttons for the key|
|value|10|The numeric value associated with this key|
|color|`"Yellow"`|Hint for rendering color of the key and/or the value, one of `"Yellow"\|"Red"\|"Blue"\|"Black"\|"White"` never null.|

Where the `ScorekeeperMatchParticipant` structure is as follows:

|property|example|description|
|--------|-------|-----------|
|federationNumber|`"NL174981"`|The federation assigned number for the participant|
|name|`"Pieter-Bas IJdens"`|The full name of the participant|
|info|`"Recurve / Heren / Klasse C"`|A concatenation of the category values for the participant for the categories that are configured for the match (in that order also)|
|categories|`[{ "id": "...", "name": "Discipline", "value": "Barebow" }]`|For each of the categories configured for the match (in that order) the currently active value for this participant|
|matchParticipantId|`"4B000000-0700-0800-1234-101010101010"`|The unique ID we use for the match participant in the scope of this match|
|tenantParticipantId|`"9F120000-0330-0550-8134-010101010101"`|The system wide unique ID we use for the match participant or `null` if unknown or not applicable|
|availableKeyIDs|`["X", "10", "9", "8", "7", "6", "M"]`|The list of key IDs that are available when entering scores for this user, based on their categories, or `null` when all keys are available|
|arrowScores|`["X", "8", "9", "10", "7", "M", null, null, null, ...]`|Ordered array of Key ID (or null if arrow has not yet been shot) values for each of the shot arrows, should be exactly `arrowsPerEnd` \* `ends` members long.|

If the device, match or tenant can't be found, are missing or invalid will return a `404` error.

## Set participants for this device

`PUT /scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}/participants`

Update the match's participants contents and order according to the posted array of partitipants. The posted array of participants is an array with `ScorekeeperMatchParticipant` elements where:
- `info` is allowed to be null and is ignored when specified, also  `availableKeyIDs` and `arrowScores` are ignored when specified and should be null.
- if `tenantParticipantId` is set, `federationNumber`, `name`, `categories` and `matchParticipantId` are ignored and should be null
- otherwise, if `matchParticipantId`, and server-side this is a *not* a record for a match-local archer (i.e. it has a null participant id at tenant-level) and any of the values for `federationNumber`, `name`, `categories` and `matchParticipantId` are different from the ones currently recorded, this returns a `409` HTTP error with error code "PARTICIPANT_UPDATE_NOT_ALLOWED"
- otherwise, if `matchParticipantId`, and server-side this is a a record for a match-local archer (i.e. it has a null participant id at tenant-level) and any of the values for `federationNumber`, `name`, `categories` and `matchParticipantId` are different from the ones currently recorded then this will update the provided value

If for the match `allowModifyParticipants` would be false in the `Get device-specific match information` call, will always return a `409` HTTP error with error code "PARTICIPANT_LIST_FIXED". There is no scenario in which this call is justified when participants may not be modified at all.

If for the match `allowCustomParticipants` would be false in the `Get device-specific match information` call and any participant in the list does not have a valid `tenantParticipantId` or a `tenantParticipantId` that's not in the server-side tenant-level participant list for the match, will return a `409` HTTP error with error code "CUSTOM_PARTICIPANT_NOT_ALLOWED"

If participants are associated with the device server-side, but they are no longer in the list, will remove that association and fix the server-side ordering. Will neither remove the participant nor its scores, none of these operations can be done using the scorekeeper interfaces.

If participants are not associated yet with the device server-side, but they are in the list, will add the association or indeed create the match-local participant data and fix the server-side ordering.

So basically this call is intended to execute all allowed modifications to the particpant lists. To get the scores for newly added participants, devices should reload the device-specific match information

## Update scores for this device

`PUT /scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}/scores`

Will send a complete snapshot of all the scores that have been *updated* on the device. The input is an array of objects, one per archer for the device, each with the following structure:

|property|example|description|
|--------|-------|-----------|
|matchParticipantId|`"4B000000-0700-0800-1234-101010101010"`|The unique ID we use for the match participant in the scope of this match|
|updates|`[{...}]`|Ordered array of `ScoreUpdate` elements|

Where a `ScoreUpdate` element is an object with this structure:
|property|example|description|
|--------|-------|-----------|
|index|15|The index of the arrow score to update for the archer|
|old|"X"|The last known value from the `arrowScores` array for this participant |
|new|"10"|The key ID of the new key that should be recorded for this arrow |

Responds either with a 2xx HTTP response OK code after having applid per participant, in order, all updated arrow values.

OR will apply all updates it can, and respond with a `409` `"UPDATE_SCORE_CONFLICT"` error, in which case the response will include a list of:

|property|example|description|
|--------|-------|-----------|
|matchParticipantId|`"4B000000-0700-0800-1234-101010101010"`|The unique ID we use for the match participant in the scope of this match|
|error|`"SCORE_CONFLICT"`|Either `"SCORE_CONFLICT"` or `"PARTICIPANT_CONFLICT"`|
|conflicts|`[{...}]`|Ordered array of `ScoreConflict` elements|

Where a `ScoreConflict` element is an object with this structure:
|property|example|description|
|--------|-------|-----------|
|index|15|The index of the arrow score the conflict occurred on|
|current|"X"|The server-side recorded value |
|old|"X"|The Key ID of the old value that the software thought was there |
|new|"10"|The key ID of the new key that was requested |

A conflict member is added (and SCORE_CONFLICT set as error code) when for a participant for an update both the `new` value from the request differs from the currently recorded arrow value at that index, *AND* the `old` value provided differs from the currently recorded arrow value at that index.

So, if currently in the system at index 15 there is a value recorded of "9" then
- if the request is `old:7`, `new:9` this is no problem (the requested value is already recorded which is perfectly okay)
- if the request is `old:9`, `new:4` this is no problem, the requesting system asks to update from 9 to 4, we can do that
- if the request is `old:8`, `new:4` this is a problem, the requesting system thinks it updates an 8 to a 4 but it would update a 9 to a 4. The update is not executed out and a conflict is returned.

For devices it makes sense to regularly invoke the `Get device-specific match information`  logic anyway to synchronize their state so they can deal with server-side changes (for example an admin linking an ad-hoc added archer to the participants list)

If there are no updates for a `matchParticipantId` it need not be in the list.

If any `matchParticipantId` is in the requested update list but not assigned to the device, ignore the score updates for that participant, and to the response add an element with an empty array of conflicts for that participant but set the error code in that object to `"PARTICIPANT_CONFLICT"` and make sure the 409 `UPDATE_SCORE_CONFLICT` error is returned.

## Get potential participants from a list

`GET /scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}/participant-options` 

There are three disjunct lists of participants that are relevant for a device, and we'll return all:
|property|example|description|
|--------|-------|-----------|
|unassigned|`[{...}]`|Array of `ScoreKeeperParticipantInfo` items for participants that are created in the match and are not yet assigned to a device|
|assigned|`[{...}]`|Array of `ScoreKeeperParticipantInfo` items for participants that are created in the match and already are assigned to a device|
|potential|`[{...}]`|Array of `ScoreKeeperParticipantInfo` items for tenant-level participants that could potentially still be added to the match.|

The `ScoreKeeperParticipantInfo` objects are structured as follows:

|property|example|description|
|--------|-------|-----------|
|matchParticipantId|`"4B000000-0700-0800-1234-101010101010"`|The unique ID we use for the match participant in the scope of this match. May be null only for items in the potential list.|
|tenantParticipantId|`"9F120000-0330-0550-8134-010101010101"`|The system wide unique ID we use for the match participant or `null` if unknown or not applicable for custom-added participants.|
|federationNumber|`"NL174981"`|The federation assigned number for the participant|
|name|`"Pieter-Bas IJdens"`|The full name of the participant|
|info|`"Recurve / Heren / Klasse C"`|A concatenation of the category values for the participant for the categories that are configured for the match (in that order also)|
|categories|`[{ "id": "...", "name": "Discipline", "value": "Barebow" }]`|For each of the categories configured for the match (in that order) the currently active value for this participant|

## Ping

`GET /scorekeeper/{tenantId:guid}/{matchId:guid}/{deviceId:guid}/time` 

Returns an object containing the server-side UTC time as an ISO string, e.g. `{ "time": "2026-08-25T12:43:56Z" }`. Can be used for playing ping-pong with the server...


