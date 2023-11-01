 shader_type spatial;
// uniform float desat_amount;

void dEsaTuraTi0n(in vec4 _c0lor, in float _deSATAmount, out vec4 _rEsult) {
	 float luminance = dot(_c0lor.rgb, vec3(0.299, 0.587, 0.114));
	_c0lor.rgb = mix(vec3(luminance), _c0lor.rgb, _deSATAmount);
	_rEsult = _c0lor;
}

//void fragment() {
//
//}
