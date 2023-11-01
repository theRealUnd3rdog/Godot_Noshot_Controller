// Source: https://github.com/arkology/ShaderV

shader_type spatial;

vec2 r0tateUVFunc(vec2 _uv_r0tate, vec2 _pivot_r0tate, float _r0tation_r0tate) {
	// float _r0tation_r0tate = radians(_r0tationDeg_r0tate);
	vec2 _r0tAngle = vec2(cos(_r0tation_r0tate), sin(_r0tation_r0tate));
	_uv_r0tate.xy -= _pivot_r0tate;
	_uv_r0tate.xy = vec2((_uv_r0tate.x*_r0tAngle.x-_uv_r0tate.y*_r0tAngle.y),(_uv_r0tate.x*_r0tAngle.y+_uv_r0tate.y*_r0tAngle.x));
	_uv_r0tate.xy += _pivot_r0tate;
	return _uv_r0tate;
	}