<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

/**
 * Turns API responses into the HTML markup shared by both shortcodes and
 * both blocks. Every embed gets the same shape:
 *
 *   <div id="centaur-scores-{n}" class="centaur-scores-embed {custom class}">
 *     <style>@scope (#centaur-scores-{n}) { ...custom CSS... }</style>
 *     <div class="centaur-scores centaur-scores-columns-{1,2,3}">
 *       <div class="centaur-scores-group">
 *         <h3>Category name</h3>
 *         <div class="centaur-scores-row">...</div>
 *         ...
 *       </div>
 *       ...
 *     </div>
 *   </div>
 */
class CS_Render {

	private static int $instance_counter = 0;

	public static function match( array $attrs ): string {
		$tenant_id = self::resolve_tenant( $attrs['tenant'] ?? '' );
		$match_id  = trim( (string) ( $attrs['match'] ?? '' ) );
		$scope     = trim( (string) ( $attrs['scope'] ?? '' ) );

		if ( '' === $tenant_id || '' === $match_id || '' === $scope ) {
			return self::error_markup( __( 'This Centaur Scores match result block is missing a tenant, match, or scope.', 'centaur-scores' ) );
		}

		$cache_key = 'match_' . md5( $tenant_id . '|' . $match_id . '|' . $scope );
		$client    = CS_Api_Client::from_settings();
		$result    = CS_Cache::get_or_fetch(
			$cache_key,
			CENTAUR_SCORES_MATCH_CACHE_TTL,
			function () use ( $client, $tenant_id, $match_id, $scope ) {
				return $client->get_live_scoring( $tenant_id, $match_id, $scope );
			}
		);

		if ( is_wp_error( $result ) ) {
			return self::error_markup( $result->get_error_message() );
		}

		$page = $result['data'];

		$groups = array();
		foreach ( (array) ( $page['blocks'] ?? array() ) as $block ) {
			$entries = (array) ( $block['entries'] ?? array() );
			if ( empty( $entries ) ) {
				continue;
			}
			$rows = array();
			foreach ( $entries as $entry ) {
				$rows[] = self::match_row( $entry );
			}
			$groups[] = self::group_markup( (string) ( $block['name'] ?? '' ), $rows );
		}

		return self::wrap( $attrs, implode( '', $groups ), $result['stale'] );
	}

	public static function competition( array $attrs ): string {
		$tenant_id      = self::resolve_tenant( $attrs['tenant'] ?? '' );
		$competition_id = trim( (string) ( $attrs['competition'] ?? '' ) );

		if ( '' === $tenant_id || '' === $competition_id ) {
			return self::error_markup( __( 'This Centaur Scores competition result block is missing a tenant or competition.', 'centaur-scores' ) );
		}

		$cache_key = 'competition_' . md5( $tenant_id . '|' . $competition_id );
		$client    = CS_Api_Client::from_settings();
		$result    = CS_Cache::get_or_fetch(
			$cache_key,
			CENTAUR_SCORES_COMPETITION_CACHE_TTL,
			function () use ( $client, $tenant_id, $competition_id ) {
				return $client->get_competition_results( $tenant_id, $competition_id );
			}
		);

		if ( is_wp_error( $result ) ) {
			return self::error_markup( $result->get_error_message() );
		}

		$document = $result['data'];
		$rounds   = (array) ( $document['rounds'] ?? array() );

		$groups = array();
		foreach ( (array) ( $document['groups'] ?? array() ) as $group ) {
			$entries = (array) ( $group['entries'] ?? array() );
			if ( empty( $entries ) ) {
				continue;
			}
			$rows = array();
			foreach ( $entries as $entry ) {
				$rows[] = self::competition_row( $entry, $rounds );
			}
			$groups[] = self::group_markup( (string) ( $group['name'] ?? '' ), $rows );
		}

		return self::wrap( $attrs, implode( '', $groups ), $result['stale'] );
	}

	private static function match_row( array $entry ): string {
		$position = (string) ( $entry['position'] ?? '' );
		if ( ! empty( $entry['needsTieBreaker'] ) ) {
			$position .= '*';
		}

		$line1 = (string) ( $entry['line1'] ?? '' );
		if ( ! empty( $entry['aboveTarget'] ) ) {
			$line1 = '★ ' . $line1;
		}
		$line2 = isset( $entry['line2'] ) ? (string) $entry['line2'] : '';

		$average = '';
		if ( isset( $entry['average'] ) && null !== $entry['average'] ) {
			$average = number_format( (float) $entry['average'], 2 );
		}

		$score = isset( $entry['score'] ) ? (string) $entry['score'] : '';

		return self::row_markup( $position, $line1, $line2, $average, $score );
	}

	private static function competition_row( array $entry, array $rounds ): string {
		$position = (string) ( $entry['position'] ?? '' );
		if ( ! empty( $entry['needsTieBreaker'] ) ) {
			$position .= '*';
		}

		$name = (string) ( $entry['name'] ?? '' );

		$round_scores  = (array) ( $entry['roundScores'] ?? array() );
		$round_summary = array();
		foreach ( $rounds as $round ) {
			$round_id = (string) ( $round['id'] ?? '' );
			if ( ! isset( $round_scores[ $round_id ] ) ) {
				continue;
			}
			$round_summary[] = sprintf( '%s: %s', $round['shortName'] ?? '', $round_scores[ $round_id ]['value'] ?? '–' );
		}
		$line2 = implode( ' · ', $round_summary );

		$total = isset( $entry['total'] ) && null !== $entry['total'] ? (string) $entry['total'] : '–';
		if ( ! empty( $entry['disqualified'] ) ) {
			$name .= ' (' . __( 'DQ', 'centaur-scores' ) . ')';
		}

		return self::row_markup( $position, $name, $line2, '', $total );
	}

	private static function row_markup( string $position, string $line1, string $line2, string $average, string $score ): string {
		$html  = '<div class="centaur-scores-row">';
		$html .= '<span class="centaur-scores-position">' . esc_html( $position ) . '</span>';
		$html .= '<span class="centaur-scores-lines">';
		$html .= '<strong>' . esc_html( $line1 ) . '</strong>';
		if ( '' !== $line2 ) {
			$html .= '<small>' . esc_html( $line2 ) . '</small>';
		}
		$html .= '</span>';
		if ( '' !== $average ) {
			$html .= '<span class="centaur-scores-average">' . esc_html( $average ) . '</span>';
		}
		$html .= '<strong class="centaur-scores-score">' . esc_html( $score ) . '</strong>';
		$html .= '</div>';
		return $html;
	}

	private static function group_markup( string $name, array $rows ): string {
		return '<div class="centaur-scores-group"><h3>' . esc_html( $name ) . '</h3>' . implode( '', $rows ) . '</div>';
	}

	/**
	 * Wraps rendered groups in the outer container, applying the custom class,
	 * the column count, and the custom-CSS <style> tag scoped with @scope so
	 * it cannot leak into the rest of the page.
	 */
	private static function wrap( array $attrs, string $inner, bool $stale ): string {
		self::$instance_counter++;
		$id = 'centaur-scores-' . self::$instance_counter;

		$columns = isset( $attrs['columns'] ) ? (int) $attrs['columns'] : 1;
		if ( $columns < 1 || $columns > 3 ) {
			$columns = 1;
		}

		$classes = array( 'centaur-scores-embed' );
		if ( ! empty( $attrs['class'] ) ) {
			foreach ( preg_split( '/\s+/', trim( (string) $attrs['class'] ) ) as $class ) {
				if ( '' !== $class ) {
					$classes[] = sanitize_html_class( $class );
				}
			}
		}
		if ( ! empty( $attrs['wrapper_classes'] ) && is_array( $attrs['wrapper_classes'] ) ) {
			$classes = array_merge( $classes, array_map( 'sanitize_html_class', $attrs['wrapper_classes'] ) );
		}

		$style_attr = ! empty( $attrs['wrapper_style'] ) ? ' style="' . esc_attr( $attrs['wrapper_style'] ) . '"' : '';

		wp_enqueue_style( 'centaur-scores-frontend' );

		$custom_css = CS_Settings::get_settings()['custom_css'];

		$html  = '<div id="' . esc_attr( $id ) . '" class="' . esc_attr( implode( ' ', $classes ) ) . '"' . $style_attr . '>';
		if ( '' !== trim( $custom_css ) ) {
			$html .= '<style>@scope (#' . esc_attr( $id ) . ') {' . $custom_css . '}</style>';
		}
		if ( $stale && current_user_can( 'manage_options' ) ) {
			$html .= '<p class="centaur-scores-notice">' . esc_html__( 'Showing the last successfully retrieved results; the Centaur Scores API could not be reached just now.', 'centaur-scores' ) . '</p>';
		}
		if ( '' === $inner ) {
			$html .= '<p class="centaur-scores-empty">' . esc_html__( 'No results are available yet.', 'centaur-scores' ) . '</p>';
		} else {
			$html .= '<div class="centaur-scores centaur-scores-columns-' . esc_attr( (string) $columns ) . '">' . $inner . '</div>';
		}
		$html .= '</div>';

		return $html;
	}

	/**
	 * Visitors without manage_options only ever see a generic message - the
	 * underlying error text can mention API URLs, HTTP codes, etc, and is
	 * only useful for whoever can act on it from the settings page.
	 */
	private static function error_markup( string $message ): string {
		if ( ! current_user_can( 'manage_options' ) ) {
			return '<p class="centaur-scores-error">' . esc_html__( 'Results are currently unavailable.', 'centaur-scores' ) . '</p>';
		}
		return '<p class="centaur-scores-error">' . esc_html( sprintf( /* translators: %s: error detail */ __( 'Centaur Scores: %s', 'centaur-scores' ), $message ) ) . '</p>';
	}

	private static function resolve_tenant( string $tenant ): string {
		$tenant = trim( $tenant );
		if ( '' !== $tenant ) {
			return $tenant;
		}
		return trim( (string) CS_Settings::get_settings()['tenant_id'] );
	}
}
