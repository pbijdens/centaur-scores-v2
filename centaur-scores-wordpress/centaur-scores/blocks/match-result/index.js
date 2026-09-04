( function ( wp ) {
	'use strict';

	var el = wp.element.createElement;
	var registerBlockType = wp.blocks.registerBlockType;
	var useBlockProps = wp.blockEditor.useBlockProps;
	var InspectorControls = wp.blockEditor.InspectorControls;
	var PanelBody = wp.components.PanelBody;
	var TextControl = wp.components.TextControl;
	var SelectControl = wp.components.SelectControl;
	var ServerSideRender = wp.serverSideRender;
	var __ = wp.i18n.__;

	registerBlockType( 'centaur-scores/match-result', {
		edit: function ( props ) {
			var attributes = props.attributes;
			var setAttributes = props.setAttributes;
			var blockProps = useBlockProps();

			return el(
				'div',
				blockProps,
				el(
					InspectorControls,
					{},
					el(
						PanelBody,
						{ title: __( 'Match settings', 'centaur-scores' ) },
						el( TextControl, {
							label: __( 'Tenant ID', 'centaur-scores' ),
							help: __( 'Leave blank to use the default tenant from Centaur Scores settings.', 'centaur-scores' ),
							value: attributes.tenantId,
							onChange: function ( value ) {
								setAttributes( { tenantId: value } );
							},
						} ),
						el( TextControl, {
							label: __( 'Match ID', 'centaur-scores' ),
							value: attributes.matchId,
							onChange: function ( value ) {
								setAttributes( { matchId: value } );
							},
						} ),
						el( TextControl, {
							label: __( 'Scope', 'centaur-scores' ),
							value: attributes.scope,
							onChange: function ( value ) {
								setAttributes( { scope: value } );
							},
						} ),
						el( SelectControl, {
							label: __( 'Columns', 'centaur-scores' ),
							value: String( attributes.columns ),
							options: [
								{ label: '1', value: '1' },
								{ label: '2', value: '2' },
								{ label: '3', value: '3' },
							],
							onChange: function ( value ) {
								setAttributes( { columns: parseInt( value, 10 ) } );
							},
						} )
					)
				),
				el( ServerSideRender, {
					block: 'centaur-scores/match-result',
					attributes: attributes,
				} )
			);
		},
		save: function () {
			return null;
		},
	} );
} )( window.wp );
