shader_type spatial;
//uniform sampler2D texture_parallax_default: hint_default_black;
uniform float heightmap_scale: hint_range(0.0, 20.0, 0.01) = 5;
uniform vec2 _depth_flip = vec2(1.0, 1.0);
uniform bool flip_heightmap = false;
//uniform sampler2D texture_albedo: source_color;

// Perform deep parallax mapping
void deep_parallax(in vec2 _uv_in, in sampler2D _texture_heightmap, in float depth_scale, in int _min_layers, in int _max_layers, in vec3 vertex, in vec3 normal, in vec3 tangent, in vec3 binormal, in vec3 eyeoffset, out vec2 uv_out) {
	vec3 view_dir = normalize(normalize(-vertex + eyeoffset) * mat3(tangent * _depth_flip.x, -binormal * _depth_flip.y, normal));
	float num_layers = mix(float(_max_layers),float(_min_layers), abs(dot(vec3(0.0, 0.0, 1.0), view_dir)));
	float layer_depth = 1.0 / num_layers;
	float current_layer_depth = 0.0;
	vec2 P = view_dir.xy * heightmap_scale * 0.01;
	vec2 delta = P / num_layers;
	vec2 ofs = _uv_in;
	float depth;
	
	if (!flip_heightmap) {
		depth = 1.0 - texture(_texture_heightmap, ofs).r;
	} else {
		depth = texture(_texture_heightmap, ofs).r;
	}
	
	float current_depth = 0.0;
	while(current_depth < depth) {
		ofs -= delta;
		if (!flip_heightmap) {
			depth = 1.0 - texture(_texture_heightmap, ofs).r;
		} else {
			depth = texture(_texture_heightmap, ofs).r;
		}
		current_depth += layer_depth;
	}
	vec2 prev_ofs = ofs + delta;
	float after_depth  = depth - current_depth;
	
	float before_depth;
	if (!flip_heightmap) {
		before_depth = ( 1.0 - texture(_texture_heightmap, prev_ofs).r  ) - current_depth + layer_depth;
	} else {
		before_depth = texture(_texture_heightmap, prev_ofs).r - current_depth + layer_depth;
	}
	
	float weight = after_depth / (after_depth - before_depth);
	ofs = mix(ofs,prev_ofs,weight);
	uv_out=ofs;
}

//void fragment() {
    //vec2 corrected_uv;
    //deep_parallax(UV, texture_parallax_default, heightmap_scale, 8, 32, VERTEX, NORMAL, TANGENT, BINORMAL, EYE_OFFSET, corrected_uv);
    //ALBEDO = texture(texture_albedo, corrected_uv).rgb;
//}