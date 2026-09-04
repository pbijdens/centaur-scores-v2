<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

/**
 * Small transient-backed cache with a "last known good" fallback.
 *
 * Every cached item is stored twice: a short-lived copy that is served as
 * long as it is fresh, and a long-lived "last good" copy that is only ever
 * read when a fresh fetch fails. This is what lets the plugin keep showing
 * the most recent successful result if the Centaur Scores API is briefly
 * unreachable, instead of showing an error where a result used to be.
 */
class CS_Cache {

	const LAST_GOOD_TTL = WEEK_IN_SECONDS;

	/**
	 * @param string   $key      Cache key, unique per distinct request (tenant/match/scope, etc).
	 * @param int      $ttl      Seconds the fresh copy is considered valid.
	 * @param callable $fetch    Callable returning the fresh value, or a WP_Error on failure.
	 * @return array{data: mixed, stale: bool}|WP_Error
	 */
	public static function get_or_fetch( string $key, int $ttl, callable $fetch ) {
		$fresh_key = 'cs_fresh_' . $key;
		$good_key  = 'cs_good_' . $key;

		$fresh = get_transient( $fresh_key );
		if ( false !== $fresh ) {
			return array(
				'data'  => $fresh,
				'stale' => false,
			);
		}

		$result = $fetch();

		if ( is_wp_error( $result ) ) {
			$last_good = get_transient( $good_key );
			if ( false !== $last_good ) {
				return array(
					'data'  => $last_good,
					'stale' => true,
				);
			}
			return $result;
		}

		set_transient( $fresh_key, $result, $ttl );
		set_transient( $good_key, $result, self::LAST_GOOD_TTL );

		return array(
			'data'  => $result,
			'stale' => false,
		);
	}
}
