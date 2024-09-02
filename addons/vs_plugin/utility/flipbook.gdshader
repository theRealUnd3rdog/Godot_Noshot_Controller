/*
*	References:
*		https://github.com/Arnklit/godot_particle_flipbook_smoothing
*/


shader_type spatial;
// render_mode unshaded, depth_draw_opaque, cull_disabled, diffuse_lambert, specular_schlick_ggx;

// uniform sampler2D flipbook_tex : source_color,filter_linear_mipmap,repeat_disable;
// uniform sampler2D flow_tex : hint_normal,filter_linear_mipmap,repeat_disable;

uniform bool _invert_x = false;
uniform bool _invert_y = false;

uniform bool _limit_frames = false;
uniform float _max_frames = 0.0;

uniform bool _anim_loop = false;
uniform bool _particle_anim = true;

uniform int _number_of_columns = 3;
uniform int _number_of_rows = 3;


void fLiPBook(vec2 _i_uv, float _playback_rate, vec4 _instance_custom, float _time, out vec2 _result_uv, out vec3 _lerpframe_data) {
	float total_frames = float(_number_of_columns * _number_of_rows);
	float h_frames = float(_number_of_columns);
	float v_frames = float(_number_of_rows);
	float current_frame;

	if (_particle_anim) {
		current_frame = floor(_instance_custom.z * total_frames);
	} else {
		current_frame = floor(_playback_rate * _time * total_frames);
	}

	if (!_limit_frames) {
		if (!_anim_loop) {
			// Clamp the frame within the valid frame range
			current_frame = clamp(current_frame, 0.0, total_frames - 1.0);
		} else {
			// Use modulo to wrap the frame within the range
			current_frame = mod(current_frame, total_frames);
		}
	} else {
		if (!_anim_loop) {
			current_frame = clamp(current_frame, 0.0, _max_frames - 1.0);
		} else {
			current_frame = mod(current_frame, _max_frames);
		}
	}

	if (_invert_x) {
		_i_uv.x = 1.0 - _i_uv.x;
	}
	if (_invert_y) {
		_i_uv.y = 1.0 - _i_uv.y;
	}

	// Scale down the base UV coordinates based on the number of columns and rows
	_i_uv /= vec2(h_frames, v_frames);

	vec2 next_uv = _i_uv;
	next_uv += vec2(mod(current_frame + 1.0, h_frames) / h_frames, floor((current_frame + 1.0 + 0.5) / h_frames) / v_frames);

	float lerpfactor = fract(_instance_custom.y * h_frames * v_frames);

	// Adjusts the UV coordinates based on the current frame position in the flipbook texture
	_i_uv += vec2(mod(current_frame, h_frames) / h_frames, floor((current_frame + 0.5) / h_frames) / v_frames);

	_result_uv = _i_uv;
	_lerpframe_data.x = lerpfactor;
	_lerpframe_data.yz = next_uv;
}


// void lerPframe(vec2 _uv_in, vec4 _color_in, vec3 _lerpframe_data, sampler2D _albedo_in, sampler2D _flow_in, out vec4 _albedo_out) {
// 	float timer = _lerpframe_data.x;
// 	vec2 next_uv = _lerpframe_data.yz;

// 	vec4 albedo_tex;
// 	if (_enable_flowmap) {
// 		vec2 flow_tex = texture(_flow_in, _uv_in).rg;
// 		flow_tex -= 0.5;
// 		flow_tex *= 2.0;
// 		vec2 flow_uv = _uv_in + flow_tex * timer * -_flow_strength;
// 		vec2 reverse_flow_uv = next_uv + flow_tex * (1.0 - timer) * _flow_strength;
// 		albedo_tex = mix(texture(_albedo_in, flow_uv), texture(_albedo_in, reverse_flow_uv), timer);
// 	} else {
// 		albedo_tex = texture(_albedo_in, _uv_in);
// 	}

// 	albedo_tex *= _color_in;

// 	_albedo_out.rgb = _albedo.rgb * albedo_tex.rgb;
// 	_albedo_out.a *= _albedo.a * albedo_tex.a;

// }


// void vertex() {
// 	vec2 ouv;
// 	vec3 framelerp_data;
// 	//float timer;
// 	fLiPBook(UV, 1.0, INSTANCE_CUSTOM, TIME, ouv, framelerp_data);
// 	//_timer = framelerp_data.x;
// 	//_next_uv = framelerp_data.yz;
// 	data = framelerp_data;
// 	 UV = ouv;
// }

// void fragment() {

// 	vec4 albedo_tex;

// 	lerPframe(UV, COLOR, data, flipbookTex, texture_flow, albedo_tex);

// 	albedo_tex *= COLOR;

// 	ALBEDO = _albedo.rgb * albedo_tex.rgb;
// 	ALPHA *= _albedo.a * albedo_tex.a;
// }
