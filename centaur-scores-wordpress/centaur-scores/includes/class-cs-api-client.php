<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

/**
 * Talks to the Centaur Scores REST API on behalf of the plugin.
 *
 * All requests (login, tenant selection, live-scoring, competition results)
 * happen server-side via wp_remote_*() - the browser never sees the
 * configured API URL, username or password, and never talks to the API
 * directly. Tenant-scoped bearer tokens are cached as transients (one per
 * tenant, since a shortcode/block can target a different tenant than the
 * configured default) so a page with several embeds does not log in again
 * for every one of them.
 */
class CS_Api_Client {

	private string $api_url;
	private string $username;
	private string $password;

	public function __construct( string $api_url, string $username, string $password ) {
		$this->api_url  = untrailingslashit( $api_url );
		$this->username = $username;
		$this->password = $password;
	}

	public static function from_settings(): self {
		$settings = CS_Settings::get_settings();
		return new self( $settings['api_url'], $settings['username'], $settings['password'] );
	}

	/**
	 * Authenticates and returns a token, expiry and account info, or a WP_Error.
	 */
	public function login() {
		if ( '' === $this->api_url || '' === $this->username || '' === $this->password ) {
			return new WP_Error( 'centaur_scores_not_configured', __( 'The Centaur Scores API is not fully configured yet.', 'centaur-scores' ) );
		}

		$response = wp_remote_post(
			$this->api_url . '/api/auth/login',
			array(
				'timeout' => 10,
				'headers' => array( 'Content-Type' => 'application/json' ),
				'body'    => wp_json_encode(
					array(
						'username' => $this->username,
						'password' => $this->password,
					)
				),
			)
		);

		return $this->decode_json_response( $response );
	}

	/**
	 * Re-mints a login token scoped to the given tenant.
	 */
	public function select_tenant( string $token, string $tenant_id ) {
		$response = wp_remote_post(
			$this->api_url . '/api/auth/select-tenant',
			array(
				'timeout' => 10,
				'headers' => array(
					'Content-Type'  => 'application/json',
					'Authorization' => 'Bearer ' . $token,
				),
				'body'    => wp_json_encode( array( 'tenantId' => $tenant_id ) ),
			)
		);

		return $this->decode_json_response( $response );
	}

	/**
	 * Returns a bearer token already scoped to $tenant_id, logging in and
	 * selecting the tenant only when there is no valid cached token.
	 *
	 * @return string|WP_Error
	 */
	public function get_tenant_token( string $tenant_id ) {
		$cache_key = 'cs_token_' . md5( $this->api_url . '|' . $this->username . '|' . $tenant_id );
		$cached    = get_transient( $cache_key );
		if ( is_string( $cached ) && '' !== $cached ) {
			return $cached;
		}

		$login = $this->login();
		if ( is_wp_error( $login ) ) {
			return $login;
		}

		$selected = $this->select_tenant( $login['token'], $tenant_id );
		if ( is_wp_error( $selected ) ) {
			return $selected;
		}

		$token = $selected['token'] ?? '';
		if ( '' === $token ) {
			return new WP_Error( 'centaur_scores_auth_failed', __( 'Authentication with the Centaur Scores API failed.', 'centaur-scores' ) );
		}

		// Keep a safety margin below the token's real expiry, and never cache
		// for longer than an hour even if the API issues longer-lived tokens.
		$ttl = 3600;
		if ( ! empty( $selected['expiresAt'] ) ) {
			$expires_at = strtotime( $selected['expiresAt'] );
			if ( false !== $expires_at ) {
				$ttl = max( 30, min( $ttl, $expires_at - time() - 30 ) );
			}
		}
		set_transient( $cache_key, $token, $ttl );

		return $token;
	}

	/**
	 * Runs a full login + tenant-selection round trip for the "Test connection"
	 * button, bypassing the token cache so the button always reflects the
	 * credentials currently in the form.
	 *
	 * @return array|WP_Error
	 */
	public function test_connection( string $tenant_id ) {
		$login = $this->login();
		if ( is_wp_error( $login ) ) {
			return $login;
		}

		if ( '' === $tenant_id ) {
			return array(
				'account' => $login['account'] ?? array(),
				'tenant'  => null,
			);
		}

		$selected = $this->select_tenant( $login['token'], $tenant_id );
		if ( is_wp_error( $selected ) ) {
			return $selected;
		}

		return array(
			'account' => $selected['account'] ?? array(),
			'tenant'  => $tenant_id,
		);
	}

	/**
	 * @return array|WP_Error Decoded LiveScoringPage.
	 */
	public function get_live_scoring( string $tenant_id, string $match_id, string $scope ) {
		$token = $this->get_tenant_token( $tenant_id );
		if ( is_wp_error( $token ) ) {
			return $token;
		}

		$response = wp_remote_get(
			$this->api_url . '/api/matches/' . rawurlencode( $match_id ) . '/live-scoring/' . rawurlencode( $scope ),
			array(
				'timeout' => 10,
				'headers' => array( 'Authorization' => 'Bearer ' . $token ),
			)
		);

		return $this->decode_json_response( $response );
	}

	/**
	 * @return array|WP_Error Decoded CompetitionResultsDocument.
	 */
	public function get_competition_results( string $tenant_id, string $competition_id ) {
		$token = $this->get_tenant_token( $tenant_id );
		if ( is_wp_error( $token ) ) {
			return $token;
		}

		$response = wp_remote_get(
			$this->api_url . '/api/competitions/' . rawurlencode( $competition_id ) . '/results',
			array(
				'timeout' => 10,
				'headers' => array( 'Authorization' => 'Bearer ' . $token ),
			)
		);

		return $this->decode_json_response( $response );
	}

	/**
	 * @return array|WP_Error
	 */
	private function decode_json_response( $response ) {
		if ( is_wp_error( $response ) ) {
			return $response;
		}

		$code = wp_remote_retrieve_response_code( $response );
		$body = wp_remote_retrieve_body( $response );
		$data = json_decode( $body, true );

		if ( $code < 200 || $code >= 300 ) {
			$message = is_array( $data ) && ! empty( $data['message'] )
				? $data['message']
				: sprintf( /* translators: %d: HTTP status code returned by the API. */ __( 'The Centaur Scores API returned an unexpected response (HTTP %d).', 'centaur-scores' ), $code );
			return new WP_Error( 'centaur_scores_api_error', $message, array( 'status' => $code, 'code' => is_array( $data ) ? ( $data['code'] ?? '' ) : '' ) );
		}

		if ( ! is_array( $data ) ) {
			return new WP_Error( 'centaur_scores_invalid_response', __( 'The Centaur Scores API returned a response that could not be understood.', 'centaur-scores' ) );
		}

		return $data;
	}
}
