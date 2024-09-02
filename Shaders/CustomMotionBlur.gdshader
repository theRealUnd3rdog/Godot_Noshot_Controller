shader_type spatial;
render_mode depth_test_disabled, depth_draw_never, unshaded, cull_disabled;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture;
uniform float motion_blur_strength = 0.5;
uniform int num_samples = 16;
uniform vec3 angular_velocity; // Camera's angular velocity
uniform vec3 linear_velocity;

void fragment()
{
    vec2 screenUV = SCREEN_UV;

    vec3 accumulatedColor = vec3(0.0, 0.0, 0.0);

    // Calculate the total velocity, combining linear and angular velocity
    vec2 total_velocity = linear_velocity.xy + angular_velocity.xy * screenUV;

    // Accumulate color samples with velocity reconstruction
    for (int i = 0; i < num_samples; i++) {
        float exposure = float(i) / float(num_samples);

        vec2 sampleOffset = total_velocity * motion_blur_strength * exposure;

        vec3 sampleColor = texture(SCREEN_TEXTURE, screenUV - sampleOffset).rgb;
        accumulatedColor += sampleColor;
    }

    vec3 motionBlurredColor = accumulatedColor / float(num_samples);

    ALBEDO = motionBlurredColor;
}
