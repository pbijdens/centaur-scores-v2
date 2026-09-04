<?php
/**
 * Uninstall handler: removes the plugin's option and any cached transients.
 * Runs only when the plugin is deleted from the Plugins screen, never on a
 * plain deactivate.
 */

if ( ! defined( 'WP_UNINSTALL_PLUGIN' ) ) {
	exit;
}

global $wpdb;

delete_option( 'centaur_scores_settings' );

// Transient names all start with cs_fresh_, cs_good_ or cs_token_ (see
// includes/class-cs-cache.php and class-cs-api-client.php); clean up both
// the values and their _transient_timeout_ companions in one pass.
$prefixes = array( 'cs_fresh_', 'cs_good_', 'cs_token_' );
foreach ( $prefixes as $prefix ) {
	$wpdb->query(
		$wpdb->prepare(
			"DELETE FROM {$wpdb->options} WHERE option_name LIKE %s OR option_name LIKE %s",
			$wpdb->esc_like( '_transient_' . $prefix ) . '%',
			$wpdb->esc_like( '_transient_timeout_' . $prefix ) . '%'
		)
	);
}
