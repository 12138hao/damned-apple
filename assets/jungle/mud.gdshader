// 泥浆表面动画：让 mud_only.png 这张静态图「活」起来。
//
// 效果顺序很重要，这里踩过坑：
//   第一版把流光加在「浑浊压暗」之前，结果被后面那次乘法直接乘没了，
//   屏幕上几乎看不出变化，看起来就是一张死图。现在改成先压暗、再加流光。
//
// 第二版又踩了一个坑，记在这里免得再犯：
//   纵向起伏 wobble_y 只给了 0.003（UV），而泥浆图层在屏幕上只有 142 像素高，
//   0.003 × 142 ≈ 0.43 像素 —— 小于一个像素，再经过 Nearest 取整，等于没动。
//   所以「泥浆没有浮动」不是效果没写，是幅度小到看不见。现在按「要几像素」反推 UV：
//   UV 幅度 = 目标像素 / 图层高度。想要 ±4 像素 → 4 / 142.7 ≈ 0.028。
//
// 三层效果：
//   ① 表面起伏 —— UV 位移。项目纹理过滤是 Nearest，位移会让像素整块跳，
//      正好是像素风要的「蠕动」质感，不会像双线性那样糊成一片。
//   ② 表面流光 —— 冷灰亮带沿 X 缓慢移动，模拟泥浆表面的光泽在流。
//      注意别开太大：泥浆本身是近黑色（#0E0D0F），加法流光一强就变灰白水面。
//   ③ 内部翻涌 —— 低频明暗脉动，像底下有东西在搅。

shader_type canvas_item;
render_mode blend_mix;

// ① 表面起伏（单位是 UV，换算见文件头注释）
uniform float wobble_x : hint_range(0.0, 0.02) = 0.0040;
uniform float wobble_y : hint_range(0.0, 0.08) = 0.0260;

// ② 表面流光
uniform float sheen_strength : hint_range(0.0, 1.0) = 0.12;
uniform float sheen_freq : hint_range(1.0, 40.0) = 4.5;
uniform float sheen_speed : hint_range(0.0, 3.0) = 0.45;

// ③ 内部翻涌
uniform float pulse_strength : hint_range(0.0, 0.5) = 0.26;
uniform float pulse_speed : hint_range(0.0, 3.0) = 0.55;

// 浑浊度：泥浆不该有水面那种镜面反光
uniform float murkiness : hint_range(0.0, 1.0) = 0.9;

// 左右两端淡出的宽度（UV）。泥浆图比缺口宽一点盖住地面切边，超出的部分靠这个淡出。
uniform float edge_fade : hint_range(0.0, 0.12) = 0.036;

// 泥浆表面在贴图里的位置：mud_only.png 220 高，表面在 y=56 → v≈0.255
uniform float surface_v : hint_range(0.0, 1.0) = 0.255;
// 表面往下多深算「表层」。这个值同时也是起伏衰减的距离：
// 太大会让整块泥体跟着晃（像果冻），太小会在泥里切出一条硬边。
uniform float surface_depth : hint_range(0.05, 0.6) = 0.15;

// 前景遮罩：只画 uv.y 小于这个值的部分，剩下的丢掉。
// 同一份材质复制一份、把这个值设成「刚好盖住玩家腿」的高度，就能当作
// 盖在玩家身上的前景泥浆 —— 因为它和背景泥浆是同一个 shader、同一组参数、
// 同一个 TIME，颜色和表面起伏天然完全对齐，不会出现接缝。
// 默认 1.0 = 不裁，背景层用不到这个。
uniform float clip_below : hint_range(0.0, 1.0) = 1.0;

void fragment() {
	vec2 uv = UV;
	// 裁剪用未位移的 UV，否则表面起伏会让裁切边跟着抖，露出玩家的脚
	if (uv.y > clip_below) {
		discard;
	}

	// 先算表层权重：表面附近为 1，往下迅速衰减到 0
	float surf = 1.0 - smoothstep(surface_v, surface_v + surface_depth, uv.y);

	// ① 表面起伏。两个不同频率的正弦叠起来，避免看出单一周期。
	float wx = sin(uv.x * 24.0 - TIME * 0.9) * wobble_x
	         + sin(uv.x * 9.0 + TIME * 0.55 + 2.1) * wobble_x * 0.6;
	float wy = sin(uv.x * 15.0 + TIME * 0.7) * wobble_y
	         + sin(uv.x * 31.0 - TIME * 1.05 + 0.8) * wobble_y * 0.35;

	uv.x += wx * surf;
	uv.y += wy * surf;

	// 位移后可能采到贴图外面，夹回来
	uv = clamp(uv, vec2(0.0), vec2(1.0));

	vec4 c = texture(TEXTURE, uv);

	if (c.a < 0.02) {
		COLOR = c;
	} else {
		// 扭曲后重新算一次表层权重，跟着采样位置走
		float s2 = 1.0 - smoothstep(surface_v, surface_v + surface_depth, uv.y);

		// ② 浑浊：压暗表面、削残余高光、整体往冷灰偏。
		//    泥浆是近黑色的（实测泥体 #0E0D0F、表层 #1A181B），不是棕色。
		if (murkiness > 0.0) {
			float lum = dot(c.rgb, vec3(0.299, 0.587, 0.114));

			c.rgb *= mix(1.0, 0.55, s2 * murkiness);

			float gloss = smoothstep(0.12, 0.60, lum) * s2;
			c.rgb *= 1.0 - gloss * murkiness * 0.55;

			// 只做很小的冷色偏移，别把泥浆染成土黄
			vec3 tint = vec3(1.0 + 0.06 * murkiness,
			                 1.0 + 0.02 * murkiness,
			                 1.0 + 0.10 * murkiness);
			c.rgb *= tint;
		}

		// ③ 表面流光 —— 必须在压暗之后，否则会被乘掉。
		//    用冷灰偏蓝的白，和泥浆的冷色调一致。
		float wave = sin(uv.x * sheen_freq * 6.2831853 - TIME * sheen_speed * 6.2831853);
		wave = pow(max(wave, 0.0), 2.2);
		c.rgb += wave * sheen_strength * s2 * vec3(0.82, 0.86, 0.95);

		// ④ 内部翻涌
		float p1 = sin(TIME * pulse_speed + uv.x * 2.7 + uv.y * 9.0);
		float p2 = sin(TIME * pulse_speed * 1.63 - uv.x * 4.1 + uv.y * 5.0);
		c.rgb *= 1.0 + (p1 + p2) * 0.5 * pulse_strength;

		// 左右两端淡出，盖住与地面交界的垂直切边
		if (edge_fade > 0.0) {
			float e = smoothstep(0.0, edge_fade, UV.x)
			        * (1.0 - smoothstep(1.0 - edge_fade, 1.0, UV.x));
			c.a *= e;
		}

		COLOR = c;
	}
}
