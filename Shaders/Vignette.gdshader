shader_type canvas_item;

uniform float vignette_intensity = 0.4;
uniform float vignette_opacity : hint_range(0.0, 1.0) = 0.5;
uniform vec4 vignette_rgb : source_color = vec4(0.0, 0.0, 0.0, 1.0);

uniform sampler2D screen_vec : hint_screen_texture;

float vignette(vec2 uv){
	uv *= 1.0 - uv.xy;
	float vignette = uv.x * uv.y * 15.0;
	return pow(vignette, vignette_intensity * vignette_opacity);
}

void fragment(){
	vec4 color = texture(screen_vec, SCREEN_UV);
	vec4 text = texture(TEXTURE, UV);
	
	text.rgba *= (vignette_rgb.rgba);
	text.rgba *= (1.0 - vignette(UV));
	
	COLOR = vec4((text.rgb)*color.rgb,text.a);
}