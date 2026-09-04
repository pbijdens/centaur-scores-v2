( function ( $ ) {
	'use strict';

	$( function () {
		var $button = $( '#centaur_scores_test_button' );
		var $result = $( '#centaur_scores_test_result' );

		if ( ! $button.length ) {
			return;
		}

		$button.on( 'click', function () {
			$button.prop( 'disabled', true );
			$result.removeClass( 'is-success is-error' ).text( CentaurScoresAdmin.i18n.testing );

			$.post( CentaurScoresAdmin.ajaxUrl, {
				action: 'centaur_scores_test_connection',
				nonce: CentaurScoresAdmin.nonce,
				api_url: $( '#centaur_scores_api_url' ).val(),
				username: $( '#centaur_scores_username' ).val(),
				password: $( '#centaur_scores_password' ).val(),
				tenant_id: $( '#centaur_scores_tenant_id' ).val(),
			} )
				.done( function ( response ) {
					if ( response && response.success ) {
						$result.addClass( 'is-success' ).text( response.data.message );
					} else {
						$result.addClass( 'is-error' ).text( ( response && response.data && response.data.message ) || 'Error' );
					}
				} )
				.fail( function () {
					$result.addClass( 'is-error' ).text( 'Error' );
				} )
				.always( function () {
					$button.prop( 'disabled', false );
				} );
		} );
	} );
} )( jQuery );
