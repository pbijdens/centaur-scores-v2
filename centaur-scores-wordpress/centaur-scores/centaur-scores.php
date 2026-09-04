<?php
/**
 * Plugin Name:       Centaur Scores
 * Plugin URI:         https://net42.org/
 * Description:       Embed live Centaur Scores match results and competition results in posts and pages, via shortcodes or blocks. All API calls are made from the server; nothing is ever sent to the browser except the rendered results.
 * Version:            1.0.2
 * Requires at least:  6.4
 * Requires PHP:       7.4
 * Author:              Centaur Scores
 * Author URI:          https://net42.org/
 * License:             GPL v2 or later
 * License URI:         https://www.gnu.org/licenses/gpl-2.0.html
 * Text Domain:         centaur-scores
 * Domain Path:         /languages
 */

if ( ! defined( 'ABSPATH' ) ) {
	exit; // No direct access.
}

define( 'CENTAUR_SCORES_VERSION', '1.0.2' );
define( 'CENTAUR_SCORES_FILE', __FILE__ );
define( 'CENTAUR_SCORES_DIR', plugin_dir_path( __FILE__ ) );
define( 'CENTAUR_SCORES_URL', plugin_dir_url( __FILE__ ) );

// How long a successfully fetched result is served from cache before a fresh
// fetch is attempted again. Kept short for match results (they change while a
// match is being scored) and longer for competition results (they only change
// when a round is entered/edited).
define( 'CENTAUR_SCORES_MATCH_CACHE_TTL', 60 );
define( 'CENTAUR_SCORES_COMPETITION_CACHE_TTL', 300 );

require_once CENTAUR_SCORES_DIR . 'includes/class-cs-cache.php';
require_once CENTAUR_SCORES_DIR . 'includes/class-cs-api-client.php';
require_once CENTAUR_SCORES_DIR . 'includes/class-cs-render.php';
require_once CENTAUR_SCORES_DIR . 'includes/class-cs-shortcodes.php';
require_once CENTAUR_SCORES_DIR . 'includes/class-cs-blocks.php';
require_once CENTAUR_SCORES_DIR . 'includes/class-cs-settings.php';

/**
 * Central plugin bootstrap. Wires up the pieces above on the usual WordPress
 * hooks; kept intentionally thin, the real work lives in the classes it calls.
 */
final class Centaur_Scores_Plugin {

	private static ?Centaur_Scores_Plugin $instance = null;

	public static function instance(): Centaur_Scores_Plugin {
		if ( null === self::$instance ) {
			self::$instance = new self();
		}
		return self::$instance;
	}

	private function __construct() {
		add_action( 'init', array( $this, 'load_textdomain' ) );
		add_action( 'init', array( $this, 'register_frontend_style' ), 5 );
		add_action( 'init', array( 'CS_Shortcodes', 'register' ) );
		add_action( 'init', array( 'CS_Blocks', 'register' ) );

		if ( is_admin() ) {
			CS_Settings::instance();
		}
	}

	public function load_textdomain(): void {
		load_plugin_textdomain( 'centaur-scores', false, dirname( plugin_basename( CENTAUR_SCORES_FILE ) ) . '/languages' );
	}

	/**
	 * Registered (but not enqueued) unconditionally, in both admin and
	 * frontend, so the "style" handle referenced from block.json exists by
	 * the time WordPress' block-asset system or a shortcode needs to enqueue
	 * it.
	 */
	public function register_frontend_style(): void {
		wp_register_style( 'centaur-scores-frontend', CENTAUR_SCORES_URL . 'assets/css/frontend.css', array(), CENTAUR_SCORES_VERSION );
	}
}

Centaur_Scores_Plugin::instance();
