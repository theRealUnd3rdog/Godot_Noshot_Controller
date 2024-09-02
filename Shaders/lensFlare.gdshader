shader_type canvas_item;
render_mode blend_mix, unshaded;

uniform vec2 sun_position = vec2(0.0);
uniform vec3 tint = vec3(1.4,1.2,1.0);
uniform sampler2D noise_texture;
uniform sampler2D SCREEN_TEXTURE : hint_screen_texture;

float noise_float(float t, vec2 texResolution)
{
	return texture(noise_texture,vec2(t,0.0)/texResolution).x;
}
float noise_vec2(vec2 t, vec2 texResolution)
{
	return texture(noise_texture,t/texResolution).x;
}

vec3 lensflare(vec2 uv,vec2 pos, vec2 texResolution)
{
	vec2 main = uv-pos;
	vec2 uvd = uv*(length(uv));

	float ang = atan(main.x,main.y);
	float dist = length(main);
	dist = pow(dist,0.1);

	float n = noise_vec2(vec2(ang*16.0,dist*32.0), texResolution);

	// Do not need an artificial sun
	//float f0 = 1.0/(length(uv-pos)*16.0+1.0);
	//f0 = f0 + f0*(sin(noise_float(sin(ang*2.+pos.x)*4.0 - cos(ang*3.+pos.y), texResolution)*16.)*.1 + dist*.1 + .8);

	float f1 = max(0.01-pow(length(uv+1.2*pos),1.9),.0)*7.0;

	float f2 = max(1.0/(1.0+32.0*pow(length(uvd+0.8*pos),2.0)),.0)*00.25;
	float f22 = max(1.0/(1.0+32.0*pow(length(uvd+0.85*pos),2.0)),.0)*00.23;
	float f23 = max(1.0/(1.0+32.0*pow(length(uvd+0.9*pos),2.0)),.0)*00.21;

	vec2 uvx = mix(uv,uvd,-0.5);

	float f4 = max(0.01-pow(length(uvx+0.4*pos),2.4),.0)*6.0;
	float f42 = max(0.01-pow(length(uvx+0.45*pos),2.4),.0)*5.0;
	float f43 = max(0.01-pow(length(uvx+0.5*pos),2.4),.0)*3.0;

	uvx = mix(uv,uvd,-.4);

	float f5 = max(0.01-pow(length(uvx+0.2*pos),5.5),.0)*2.0;
	float f52 = max(0.01-pow(length(uvx+0.4*pos),5.5),.0)*2.0;
	float f53 = max(0.01-pow(length(uvx+0.6*pos),5.5),.0)*2.0;

	uvx = mix(uv,uvd,-0.5);

	float f6 = max(0.01-pow(length(uvx-0.3*pos),1.6),.0)*6.0;
	float f62 = max(0.01-pow(length(uvx-0.325*pos),1.6),.0)*3.0;
	float f63 = max(0.01-pow(length(uvx-0.35*pos),1.6),.0)*5.0;

	vec3 c = vec3(.0);

	c.r+=f2+f4+f5+f6; c.g+=f22+f42+f52+f62; c.b+=f23+f43+f53+f63;
	c = c*1.3 - vec3(length(uvd)*.05);

	// Do not need an artificial sun
	//c+=vec3(f0);

	return c;
}

vec3 cc(vec3 color, float factor,float factor2) // color modifier
{
	float w = color.x+color.y+color.z;
	return mix(color,vec3(w)*factor,w*factor2);
}

void fragment()
{
	vec2 texResolution = 1.0 / TEXTURE_PIXEL_SIZE;
	vec2 resolution = 1.0 / SCREEN_PIXEL_SIZE;

	vec2 uv = FRAGCOORD.xy / resolution.xy - 0.5;
	uv.x *= resolution.x/resolution.y; //fix aspect ratio
	vec2 mouse = (sun_position.xy / resolution.xy) - vec2(0.5, 0.5);
	mouse.x *= resolution.x / resolution.y; //fix aspect ratio

	vec4 previousColor = texture(SCREEN_TEXTURE, SCREEN_UV);

	vec3 color = previousColor.rgb;

	color += tint * lensflare(uv, mouse.xy, texResolution);
	color -= noise_vec2(FRAGCOORD.xy, texResolution)*.015;
	color = cc(color,.5,.1);

	// Reduce transparency by multiplying alpha by a factor
    float alpha = 0.5; // Adjust this value to control transparency
    COLOR = vec4(color, previousColor.a * alpha);
}