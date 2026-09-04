<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

/**
 * Registers the "Centaur Scores Match Result" and "Centaur Scores
 * Competition Result" blocks. Both are dynamic (server-rendered) blocks so
 * they share their render logic with the shortcodes via CS_Render, and both
 * preview themselves in the editor with WordPress core's ServerSideRender
 * component (no build step / bundler required for the editor scripts).
 */
class CS_Blocks {

	public static function register(): void {
		self::register_editor_scripts();

		register_block_type(
			CENTAUR_SCORES_DIR . 'blocks/match-result',
			array( 'render_callback' => array( __CLASS__, 'render_match' ) )
		);

		register_block_type(
			CENTAUR_SCORES_DIR . 'blocks/competition-result',
			array( 'render_callback' => array( __CLASS__, 'render_competition' ) )
		);
	}

	private static function register_editor_scripts(): void {
		$deps = array( 'wp-blocks', 'wp-element', 'wp-block-editor', 'wp-components', 'wp-i18n', 'wp-server-side-render' );

		wp_register_script(
			'centaur-scores-match-result-editor',
			CENTAUR_SCORES_URL . 'blocks/match-result/index.js',
			$deps,
			CENTAUR_SCORES_VERSION,
			true
		);
		wp_register_script(
			'centaur-scores-competition-result-editor',
			CENTAUR_SCORES_URL . 'blocks/competition-result/index.js',
			$deps,
			CENTAUR_SCORES_VERSION,
			true
		);

		wp_set_script_translations( 'centaur-scores-match-result-editor', 'centaur-scores', CENTAUR_SCORES_DIR . 'languages' );
		wp_set_script_translations( 'centaur-scores-competition-result-editor', 'centaur-scores', CENTAUR_SCORES_DIR . 'languages' );
	}

	public static function render_match( array $attributes ): string {
		return CS_Render::match(
			array(
				'tenant'  => $attributes['tenantId'] ?? '',
				'match'   => $attributes['matchId'] ?? '',
				'scope'   => $attributes['scope'] ?? '',
				'columns' => $attributes['columns'] ?? 1,
			) + self::wrapper_attrs( $attributes )
		);
	}

	public static function render_competition( array $attributes ): string {
		return CS_Render::competition(
			array(
				'tenant'      => $attributes['tenantId'] ?? '',
				'competition' => $attributes['competitionId'] ?? '',
				'columns'     => $attributes['columns'] ?? 1,
			) + self::wrapper_attrs( $attributes )
		);
	}

	/**
	 * Translates the block's supports-managed className/spacing attributes
	 * into the wrapper_classes/wrapper_style options CS_Render understands.
	 */
	private static function wrapper_attrs( array $attributes ): array {
		$classnames = array();
		$css        = '';

		if ( ! empty( $attributes['style'] ) && function_exists( 'wp_style_engine_get_styles' ) ) {
			$styles = wp_style_engine_get_styles( $attributes['style'], array( 'context' => 'block-supports' ) );
			if ( ! empty( $styles['css'] ) ) {
				$css = $styles['css'];
			}
			if ( ! empty( $styles['classnames'] ) ) {
				$classnames[] = $styles['classnames'];
			}
		}

		if ( ! empty( $attributes['className'] ) ) {
			$classnames[] = $attributes['className'];
		}

		$classes = array();
		foreach ( $classnames as $group ) {
			foreach ( preg_split( '/\s+/', trim( (string) $group ) ) as $class ) {
				if ( '' !== $class ) {
					$classes[] = $class;
				}
			}
		}

		return array(
			'wrapper_classes' => $classes,
			'wrapper_style'   => $css,
		);
	}
}
