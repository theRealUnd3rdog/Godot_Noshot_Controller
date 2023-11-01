/*
*	References:
*		https://github.com/Arnklit/godot_particle_flipbook_smoothing
*/

shader_type spatial;

uniform vec4 _albedo : source_color = vec4(1.0);
uniform bool _enable_flowmap = true;
uniform float _flow_strength : hint_range(0.0, 0.08, 0.001) = 0.015;

void lerPframe(vec2 _uv_in, vec4 _color_in, vec3 _lerpframe_data, sampler2D _albedo_in, sampler2D _flow_in, out vec4 _albedo_out) {
	float lerpfactor = _lerpframe_data.x;
	vec2 next_uv = _lerpframe_data.yz;
	
	vec4 albedo_tex;
	if (_enable_flowmap) {
		vec2 flow_tex = texture(_flow_in, _uv_in).rg;
		flow_tex -= 0.5;
		flow_tex *= 2.0;
		vec2 flow_uv = _uv_in + flow_tex * lerpfactor * -_flow_strength;
		vec2 reverse_flow_uv = next_uv + flow_tex * (1.0 - lerpfactor) * _flow_strength;
		albedo_tex = mix(texture(_albedo_in, flow_uv), texture(_albedo_in, reverse_flow_uv), lerpfactor);
	} else {
		albedo_tex = texture(_albedo_in, _uv_in);
	}

	albedo_tex *= _color_in;
	
	_albedo_out.rgb = _albedo.rgb * albedo_tex.rgb;
	_albedo_out.a *= _albedo.a * albedo_tex.a;

}