<?php
if ( ! defined( 'ABSPATH' ) ) {
	exit;
}

/**
 * [centaur_scores_match] and [centaur_scores_competition] shortcodes.
 */
class CS_Shortcodes {

	public static function register(): void {
		add_shortcode( 'centaur_scores_match', array( __CLASS__, 'render_match' ) );
		add_shortcode( 'centaur_scores_competition', array( __CLASS__, 'render_competition' ) );
	}

	/**
	 * [centaur_scores_match tenant="" match="" scope="" class="" columns="1"]
	 */
	public static function render_match( $atts ): string {
		$atts = shortcode_atts(
			array(
				'tenant'  => '',
				'match'   => '',
				'scope'   => '',
				'class'   => '',
				'columns' => '1',
			),
			$atts,
			'centaur_scores_match'
		);

		return CS_Render::match( $atts );
	}

	/**
	 * [centaur_scores_competition tenant="" competition="" class="" columns="1"]
	 */
	public static function render_competition( $atts ): string {
		$atts = shortcode_atts(
			array(
				'tenant'      => '',
				'competition' => '',
				'class'       => '',
				'columns'     => '1',
			),
			$atts,
			'centaur_scores_competition'
		);

		return CS_Render::competition( $atts );
	}
}
