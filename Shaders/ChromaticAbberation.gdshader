shader_type canvas_item;

uniform sampler2D offset_image : hint_default_white;
uniform float strength = 1.0;
uniform sampler2D SCREEN_TEXTURE:hint_screen_texture, filter_linear_mipmap;

void fragment() {
	vec4 output = texture(SCREEN_TEXTURE, SCREEN_UV);
	
	float shift = strength * texture(offset_image, SCREEN_UV).r / 100.0;
	output.r = texture(SCREEN_TEXTURE, vec2(SCREEN_UV.x + shift, SCREEN_UV.y)).r;
	output.g = texture(SCREEN_TEXTURE, SCREEN_UV).g;
	output.b = texture(SCREEN_TEXTURE, vec2(SCREEN_UV.x - shift, SCREEN_UV.y)).b;
	
	COLOR = output;
}