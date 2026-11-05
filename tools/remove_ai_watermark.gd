extends SceneTree

# 抹掉 mud_cut_final.png 右下角的「AI生成」标签。
#
# 做法：标签下面是同一段碎石纹理，所以从左边 130 像素处借一块同样大小的补丁盖过去，
# 边缘做 6 像素羽化，避免出现硬边。
#
# 标签实测边界 x[1943,2027] y[461,495]，四周各留 6 像素余量后：
#   目标区 x[1932,2047] y[452,506]
#   来源区 x[1802,1917] y[452,506]（整体左移 130）

const SRC_PATH := "res://mud_cut_final.png"
const BAK_PATH := "res://tools/mud_cut_final.png.bak_wm"
const OUT_PATH := "res://tools/_sd/mud_cut_final_clean.png"

const TX0 := 1932
const TY0 := 452
const TX1 := 2048      # 右边界不含
const TY1 := 507
const SHIFT := 130
const FEATHER := 6

func _initialize() -> void:
	var img := Image.load_from_file(ProjectSettings.globalize_path(SRC_PATH))
	if img == null:
		print("读不到源图"); quit(); return
	if img.get_format() != Image.FORMAT_RGBA8:
		img.convert(Image.FORMAT_RGBA8)
	var w := img.get_width()
	var h := img.get_height()
	print("源图 %dx%d" % [w, h])

	# 先把原图另存一份原始数据做备份（用 Image 另存，保留原样）
	var bak := img.duplicate() as Image
	bak.save_png(BAK_PATH)
	print("已备份 -> %s" % BAK_PATH)

	# 借补丁 + 羽化
	var changed := 0
	for y in range(TY0, TY1):
		for x in range(TX0, TX1):
			var sx := x - SHIFT
			if sx < 0: continue
			var src := img.get_pixel(sx, y)
			# 到目标区四边的距离，取最小
			var d: int = min(min(x - TX0, TX1 - 1 - x), min(y - TY0, TY1 - 1 - y))
			var a: float = clamp(float(d) / float(FEATHER), 0.0, 1.0)
			if a <= 0.0: continue
			var dst := img.get_pixel(x, y)
			img.set_pixel(x, y, dst.lerp(src, a))
			changed += 1
	print("已替换 %d 个像素（目标区 %dx%d，左移 %d 借补丁，羽化 %d）" % [
		changed, TX1 - TX0, TY1 - TY0, SHIFT, FEATHER])

	img.save_png(OUT_PATH)
	print("输出 -> %s" % OUT_PATH)

	# 顺手把修补区放大 12 倍存一张，方便肉眼复核
	var z := img.get_region(Rect2i(1878, 446, 170, 66))
	z.resize(170 * 12, 66 * 12, Image.INTERPOLATE_NEAREST)
	z.save_png("res://tools/_sd/wm_after.png")
	print("复核图 -> tools/_sd/wm_after.png")
	quit()
