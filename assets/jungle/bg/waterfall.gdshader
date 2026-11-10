shader_type canvas_item;

// 瀑布水幕：纵向滚动 + 轻微横向摆动。
//
// 用之前必须把 Sprite2D 的 texture_repeat 设成 Enabled，
// 这样 uv 超出 0~1 时会自动平铺，一张贴图就能无限往下流。
//
// 关键：每块水幕单独给一个 phase，错开相位。
// 不然整片瀑布是同一个节奏在动，一眼就能看出是"同一张图贴了 N 遍"。

uniform vec2 tiles = vec2(1.0, 1.0);   // 这张精灵里横竖各重复几次（一格就是 1,1）
uniform float speed = 0.16;            // 每秒流下多少个贴图高度
uniform float wobble_amp = 0.0035;     // 横向摆动幅度（UV，按贴图宽度算）
uniform float wobble_freq = 5.0;       // 摆动频率
uniform float phase = 0.0;             // 纵向相位偏移，每块随机
uniform float shift_x = 0.0;           // 横向偏移，每块随机 —— 让相邻两块不是同一个图案

void fragment() {
	vec2 uv = UV * tiles;
	uv.x += shift_x;
	// 注意是减号：uv 越大越靠下，采样坐标随时间增大 = 图案往屏幕上方跑。
	// 想让它往下流，采样坐标必须随时间变小。
	uv.y -= phase + TIME * speed;
	uv.x += sin(uv.y * wobble_freq + phase * 6.2831) * wobble_amp;
	COLOR = texture(TEXTURE, uv);
}
