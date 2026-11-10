shader_type canvas_item;


// 水流冲击波：**完全程序化生成**，不贴任何贴图。
//
// 为什么要这么写：
//   原来是把一张 water_beam.png 沿长度重复（tiles）再滚动，只要光束比贴图长，
//   就一定会出现"同一段水纹又来了"的拼接感，横向还能看到接缝。
//   这里改成用 sin 叠加直接算水纹 —— 整条光束是连续的一整条，天然没有接缝，
//   而且长度、粗细怎么改都不会变形。
//
// 用法：Sprite2D 挂一张 1x1 白色贴图，Scale 拉到 (长度, 粗细)，
//       UV 就是 0..1 铺满整条光柱。
//       wave_count 由 C# 按「长度 / 190」传进来，保证波长恒定。

uniform vec3 core_color : source_color = vec3(0.62, 0.92, 1.00); // 柱心（亮青）
uniform vec3 body_color : source_color = vec3(0.10, 0.52, 1.00); // 柱身（饱和亮蓝）
uniform float energy = 1.0;        // 整体亮度（1 = 正常）
uniform float wave_count = 8.0;    // 整条光束上有几个波的循环
uniform float flow_speed = 3.4;    // 水纹往前推进的速度
uniform float head_boost = 1.0;    // 前端（撞击处）额外亮度

// 三层 sin 叠出来的伪噪声，够像水的涌动，又不用真的噪声函数
float wv(float t) {
	return sin(t) * 0.52 + sin(t * 2.31 + 1.7) * 0.30 + sin(t * 3.97 + 3.9) * 0.18;
}

void fragment() {
	// x：0 = 从法阵出发的一端，1 = 前端；y：-1..1 是横向
	float x = UV.x;
	float y = (UV.y - 0.5) * 2.0;
	float ay = abs(y);

	// ① 光柱截面：中间一圈亮心，外面是较宽的蓝色水体，最外侧是白浪
	float core = 1.0 - smoothstep(0.0, 0.55, ay);
	float body = 1.0 - smoothstep(0.30, 1.02, ay);

	// ② 沿长度往前推的水纹，相位随 y 偏移 → 形成斜向涌动的层次
	float p = x * wave_count * 6.2831853;
	float t = TIME * flow_speed;
	float w1 = wv(p - t + y * 2.4);
	float w2 = wv(p * 1.9 - t * 1.6 - y * 3.2 + 2.1);
	float ripple = 0.5 + 0.5 * (w1 * 0.6 + w2 * 0.4);

	// ③ 边缘浪花：贴着上下沿跑，越靠边越白
	float edge = smoothstep(0.42, 0.86, ay) * (1.0 - smoothstep(0.86, 1.02, ay));
	float foam = edge * (0.45 + 0.55 * wv(p * 3.1 - t * 3.4 + y * 7.0));

	// ④ 前端更亮，像水撞出去那一下
	float head = smoothstep(0.80, 1.0, x) * head_boost;

	vec3 col = mix(body_color, core_color, core * 0.85);
	col = mix(col, vec3(0.95, 0.99, 1.0), foam * 0.8);      // 边缘是白浪
	col += core_color * (ripple * 0.22 * body);
	col += core_color * head * 0.20;

	float a = body * (0.62 + 0.38 * ripple);
	a = clamp(a + foam * 0.55, 0.0, 1.0);

	COLOR = vec4(col * energy, a * energy);
}
