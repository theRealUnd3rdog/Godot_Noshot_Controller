shader_type canvas_item;

// 発射中央部
uniform vec2 blur_center = vec2( 0.5, 0.5 );
// ブラー強度
uniform float blur_power : hint_range( 0.0, 0.026 ) = 0.01;
// サンプリング回数
uniform int sampling_count : hint_range( 1, 64 ) = 6;
uniform sampler2D SCREEN_TEXTURE: hint_screen_texture;

void fragment( )
{
	vec2 direction = SCREEN_UV - blur_center;
	vec3 c = vec3( 0.0, 0.0, 0.0 );
	float f = 1.0 / float( sampling_count );
	for( int i=0; i < sampling_count; i++ ) {
		c += texture( SCREEN_TEXTURE, SCREEN_UV - blur_power * direction * float(i) ).rgb * f;
	}
	COLOR.rgb = c;
}