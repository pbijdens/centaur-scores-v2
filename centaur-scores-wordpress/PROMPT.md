In this centaur-scores-wordpress folder, please create a wordpress plug-in with the following properties:

1. Follows the website language for English or Dutch
2. Settings page where we can configure
   1. URL of the API
   2. Username
   3. Password
   4. Default tenant ID
   5. Test button that tests authentication
   6. Custom CSS to be included in each embedded block scoped to that block 
3. Supports a shortcode that can be used to embed single-match results
   1. Parameters
      1. Tenant ID
      2. Match ID
      3. Scope
      4. Custom CSS class
   2. Output
      1. Outer outer block containing the 
         1. Outer block with as class name "centaur-scores"
            1. One block per category group
               1. One line per reslt, just like in the 2-column output for the scores containing whatever is configured in the scope
   3. Uses the `/api/matches/{id}/live-scoring/{scope}` API, authentication happens from PHP *not* from the client
4. Supports a shortcode that can be used to embed competition results
   1. Parameters
      1. Tenant ID
      2. Competition ID
      3. Custom CSS class
   2. Output
      1. Outer outer block containing the 
         1. Outer block with as class name "centaur-scores"
            1. One block per category group
               1. One line per reslt, just like in the 2-column output for the scores containing whatever is configured in the scope
   3. Uses the `/api/competitions/{id}/results` API, authentication happens from PHP *not* from the client
5. If possible, supports blocks for the Wordpress block editor that have the same effect as the shortcodes, call these "Centaur Scores Match Result" and "Centaur Scores Competition Result" and support preview in the visual editor
   1. Supports common rendering options for blocks such as margins
   2. Supports 1, 2 or 3 column output for any result block, defaults to 1
6. Any and all API calls to the centaur scores API are placed from within PHP never from the client
   1. Results are cached. If later calls fail the last cached result is returned

Author should be "Centaur Scores" and users should be directed to https://net42.org/ for more information.