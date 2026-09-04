<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

/**
 * Settings > Centaur Scores admin page: API connection details, the default
 * tenant, custom CSS, and the "Test connection" button.
 */
class CS_Settings {

	const OPTION_NAME = 'centaur_scores_settings';
	const NONCE_ACTION = 'centaur_scores_test_connection';

	private static ?CS_Settings $instance = null;

	public static function instance(): CS_Settings {
		if ( null === self::$instance ) {
			self::$instance = new self();
		}
		return self::$instance;
	}

	private function __construct() {
		add_action( 'admin_menu', array( $this, 'add_settings_page' ) );
		add_action( 'admin_init', array( $this, 'register_settings' ) );
		add_action( 'admin_enqueue_scripts', array( $this, 'enqueue_admin_assets' ) );
		add_action( 'wp_ajax_centaur_scores_test_connection', array( $this, 'ajax_test_connection' ) );
	}

	/**
	 * Returns the stored settings merged with defaults, always as strings.
	 */
	public static function get_settings(): array {
		$defaults = array(
			'api_url'    => '',
			'username'   => '',
			'password'   => '',
			'tenant_id'  => '',
			'custom_css' => '',
		);
		$stored = get_option( self::OPTION_NAME, array() );
		if ( ! is_array( $stored ) ) {
			$stored = array();
		}
		return array_merge( $defaults, $stored );
	}

	public function add_settings_page(): void {
		add_options_page(
			__( 'Centaur Scores', 'centaur-scores' ),
			__( 'Centaur Scores', 'centaur-scores' ),
			'manage_options',
			'centaur-scores',
			array( $this, 'render_settings_page' )
		);
	}

	public function enqueue_admin_assets( string $hook ): void {
		if ( 'settings_page_centaur-scores' !== $hook ) {
			return;
		}
		wp_enqueue_style( 'centaur-scores-admin', CENTAUR_SCORES_URL . 'assets/css/admin.css', array(), CENTAUR_SCORES_VERSION );
		wp_enqueue_script( 'centaur-scores-admin', CENTAUR_SCORES_URL . 'assets/js/admin.js', array( 'jquery' ), CENTAUR_SCORES_VERSION, true );
		wp_localize_script(
			'centaur-scores-admin',
			'CentaurScoresAdmin',
			array(
				'ajaxUrl' => admin_url( 'admin-ajax.php' ),
				'nonce'   => wp_create_nonce( self::NONCE_ACTION ),
				'i18n'    => array(
					'testing' => __( 'Testing…', 'centaur-scores' ),
					'button'  => __( 'Test authentication', 'centaur-scores' ),
				),
			)
		);
	}

	public function register_settings(): void {
		register_setting(
			'centaur_scores',
			self::OPTION_NAME,
			array(
				'type'              => 'array',
				'sanitize_callback' => array( $this, 'sanitize_settings' ),
				'default'           => array(),
			)
		);

		add_settings_section( 'centaur_scores_main', '', '__return_false', 'centaur-scores' );

		add_settings_field( 'api_url', __( 'API URL', 'centaur-scores' ), array( $this, 'field_api_url' ), 'centaur-scores', 'centaur_scores_main' );
		add_settings_field( 'username', __( 'Username', 'centaur-scores' ), array( $this, 'field_username' ), 'centaur-scores', 'centaur_scores_main' );
		add_settings_field( 'password', __( 'Password', 'centaur-scores' ), array( $this, 'field_password' ), 'centaur-scores', 'centaur_scores_main' );
		add_settings_field( 'tenant_id', __( 'Default tenant ID', 'centaur-scores' ), array( $this, 'field_tenant_id' ), 'centaur-scores', 'centaur_scores_main' );
		add_settings_field( 'test_connection', __( 'Connection', 'centaur-scores' ), array( $this, 'field_test_connection' ), 'centaur-scores', 'centaur_scores_main' );
		add_settings_field( 'custom_css', __( 'Custom CSS', 'centaur-scores' ), array( $this, 'field_custom_css' ), 'centaur-scores', 'centaur_scores_main' );
	}

	/**
	 * Keeps the previously saved password when the field is left blank, so the
	 * admin does not have to re-enter it every time they touch another field.
	 */
	public function sanitize_settings( $input ): array {
		$existing = self::get_settings();
		$input    = is_array( $input ) ? $input : array();

		$password = isset( $input['password'] ) ? (string) $input['password'] : '';
		if ( '' === $password ) {
			$password = $existing['password'];
		}

		return array(
			'api_url'    => isset( $input['api_url'] ) ? untrailingslashit( esc_url_raw( trim( (string) $input['api_url'] ) ) ) : '',
			'username'   => isset( $input['username'] ) ? sanitize_text_field( $input['username'] ) : '',
			'password'   => $password,
			'tenant_id'  => isset( $input['tenant_id'] ) ? sanitize_text_field( $input['tenant_id'] ) : '',
			'custom_css' => isset( $input['custom_css'] ) ? wp_strip_all_tags( $input['custom_css'] ) : '',
		);
	}

	public function field_api_url(): void {
		$settings = self::get_settings();
		printf(
			'<input type="url" class="regular-text" id="centaur_scores_api_url" name="%1$s[api_url]" value="%2$s" placeholder="https://scores.example.org" /><p class="description">%3$s</p>',
			esc_attr( self::OPTION_NAME ),
			esc_attr( $settings['api_url'] ),
			esc_html__( 'The base URL to use in front of every API path (/api/auth/login, /api/matches/..., etc) exactly as it needs to appear in the request - if your Centaur Scores API is reverse-proxied under an /api path, include that here too.', 'centaur-scores' )
		);
	}

	public function field_username(): void {
		$settings = self::get_settings();
		printf(
			'<input type="text" class="regular-text" id="centaur_scores_username" name="%1$s[username]" value="%2$s" autocomplete="off" />',
			esc_attr( self::OPTION_NAME ),
			esc_attr( $settings['username'] )
		);
	}

	public function field_password(): void {
		printf(
			'<input type="password" class="regular-text" id="centaur_scores_password" name="%1$s[password]" value="" autocomplete="new-password" placeholder="%2$s" /><p class="description">%3$s</p>',
			esc_attr( self::OPTION_NAME ),
			esc_attr__( 'Leave blank to keep the currently saved password', 'centaur-scores' ),
			esc_html__( 'Stored in the WordPress database and only ever used server-side.', 'centaur-scores' )
		);
	}

	public function field_tenant_id(): void {
		$settings = self::get_settings();
		printf(
			'<input type="text" class="regular-text" id="centaur_scores_tenant_id" name="%1$s[tenant_id]" value="%2$s" placeholder="00000000-0000-0000-0000-000000000000" /><p class="description">%3$s</p>',
			esc_attr( self::OPTION_NAME ),
			esc_attr( $settings['tenant_id'] ),
			esc_html__( 'Used whenever a shortcode or block does not specify its own tenant ID.', 'centaur-scores' )
		);
	}

	public function field_test_connection(): void {
		echo '<button type="button" class="button" id="centaur_scores_test_button">' . esc_html__( 'Test authentication', 'centaur-scores' ) . '</button>';
		echo '<span id="centaur_scores_test_result" role="status" aria-live="polite"></span>';
		echo '<p class="description">' . esc_html__( 'Tests the fields above without needing to save first.', 'centaur-scores' ) . '</p>';
	}

	public function field_custom_css(): void {
		$settings = self::get_settings();
		printf(
			'<textarea class="large-text code" rows="10" id="centaur_scores_custom_css" name="%1$s[custom_css]" placeholder="%2$s">%3$s</textarea><p class="description">%4$s</p>',
			esc_attr( self::OPTION_NAME ),
			esc_attr__( ".centaur-scores-group h3 { color: #164a13; }", 'centaur-scores' ),
			esc_textarea( $settings['custom_css'] ),
			esc_html__( 'Applied only inside each Centaur Scores embed, never to the rest of the page.', 'centaur-scores' )
		);
	}

	public function render_settings_page(): void {
		if ( ! current_user_can( 'manage_options' ) ) {
			return;
		}
		?>
		<div class="wrap centaur-scores-settings">
			<h1><?php esc_html_e( 'Centaur Scores', 'centaur-scores' ); ?></h1>
			<p>
				<?php
				printf(
					/* translators: %s: link to net42.org */
					esc_html__( 'Configure the connection to your Centaur Scores API. For more information, visit %s.', 'centaur-scores' ),
					'<a href="https://net42.org/" target="_blank" rel="noopener noreferrer">net42.org</a>'
				);
				?>
			</p>
			<form action="options.php" method="post">
				<?php
				settings_fields( 'centaur_scores' );
				do_settings_sections( 'centaur-scores' );
				submit_button();
				?>
			</form>

			<hr />
			<h2><?php esc_html_e( 'Usage', 'centaur-scores' ); ?></h2>
			<p><?php esc_html_e( 'Embed a single match result:', 'centaur-scores' ); ?></p>
			<code>[centaur_scores_match match="MATCH_ID" scope="SCOPE_NAME"]</code>
			<p><?php esc_html_e( 'Embed competition results:', 'centaur-scores' ); ?></p>
			<code>[centaur_scores_competition competition="COMPETITION_ID"]</code>
			<p>
				<?php esc_html_e( 'Both shortcodes also accept tenant and class attributes, and are available as blocks named "Centaur Scores Match Result" and "Centaur Scores Competition Result" in the block editor.', 'centaur-scores' ); ?>
			</p>
		</div>
		<?php
	}

	public function ajax_test_connection(): void {
		check_ajax_referer( self::NONCE_ACTION, 'nonce' );

		if ( ! current_user_can( 'manage_options' ) ) {
			wp_send_json_error( array( 'message' => __( 'You are not allowed to do this.', 'centaur-scores' ) ), 403 );
		}

		$existing  = self::get_settings();
		$api_url   = isset( $_POST['api_url'] ) ? esc_url_raw( wp_unslash( $_POST['api_url'] ) ) : '';
		$username  = isset( $_POST['username'] ) ? sanitize_text_field( wp_unslash( $_POST['username'] ) ) : '';
		$password  = isset( $_POST['password'] ) ? (string) wp_unslash( $_POST['password'] ) : '';
		$tenant_id = isset( $_POST['tenant_id'] ) ? sanitize_text_field( wp_unslash( $_POST['tenant_id'] ) ) : '';

		if ( '' === $password ) {
			$password = $existing['password'];
		}

		$client = new CS_Api_Client( $api_url, $username, $password );
		$result = $client->test_connection( $tenant_id );

		if ( is_wp_error( $result ) ) {
			wp_send_json_error( array( 'message' => $result->get_error_message() ) );
		}

		$account_name = $result['account']['displayName'] ?? ( $result['account']['username'] ?? $username );

		if ( null === $result['tenant'] ) {
			wp_send_json_success(
				array(
					'message' => sprintf(
						/* translators: %s: account display name */
						__( 'Login succeeded as %s. Add a default tenant ID to also verify tenant access.', 'centaur-scores' ),
						$account_name
					),
				)
			);
		}

		wp_send_json_success(
			array(
				'message' => sprintf(
					/* translators: %s: account display name */
					__( 'Success: authenticated as %s and confirmed access to the tenant.', 'centaur-scores' ),
					$account_name
				),
			)
		);
	}
}
