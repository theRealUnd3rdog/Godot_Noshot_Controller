shader_type canvas_item;
render_mode blend_mix, unshaded;

uniform float zoom = 300;
uniform bool pDir = false;
uniform bool noWrap = true;
uniform sampler2D SCREEN_TEXTURE: hint_screen_texture, filter_linear_mipmap;


void fragment() {
  float fB;
  float fC;


  vec2 posTex;
  vec4 color = vec4(0.0, 0.0, 0.0, 1.0);
  vec2 uv = SCREEN_UV;
vec2 fPixel = SCREEN_PIXEL_SIZE;

  if(pDir == false){

    fB = 1.0 - (zoom * fPixel.y);
    fC = max(0.02, 1.0 + (fB - 1.0) * 4.0 * pow((uv.x - 0.5), 2.0));

    posTex = uv * vec2(1.0, fC) + vec2(0.0, (1.0 - fC) / 2.0);

    if(noWrap == false || (posTex.y >= 0.0 && posTex.y <= 1.0)){
      COLOR.rgb = textureLod(SCREEN_TEXTURE, posTex, 0.0).rgb;
    }
  } else {
    fB = 1.0 - (zoom * fPixel.x);
    fC = max(0.05, 1.0 + (fB - 1.0) * 4.0 * pow((uv.y - 0.5), 2.0));

    posTex = uv * vec2(fC, 1.0) + vec2((1.0 - fC) / 2.0, 0.0);

    if(noWrap == false || (posTex.x >= 0.0 && posTex.x <= 1.0)){
      COLOR.rgb = textureLod(SCREEN_TEXTURE, posTex, 0.0).rgb;
    }
  }
}