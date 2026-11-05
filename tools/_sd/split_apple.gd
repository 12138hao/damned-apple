extends SceneTree

# 把苹果怪贴图按连通域拆成三张独立图：身体（含海盗帽）/ 左手 / 右手。
# 输出到 assets/enemies/undead_apple/

const SRC_DIR := "D:/workbuddyhuancun/2026-11-05-15-03-54/pixel-apple/"
const OUT_DIR := "D:/h/my-game/assets/enemies/undead_apple/"

func _load(path: String) -> Image:
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null: return null
	var img := Image.new()
	if img.load_png_from_buffer(f.get_buffer(f.get_length())) != OK: return null
	if img.is_compressed(): img.decompress()
	img.convert(Image.FORMAT_RGBA8)
	return img

# 返回每个像素的连通域编号（-1 = 透明）
func _label(img: Image, thresh: int) -> Dictionary:
	var w := img.get_width()
	var h := img.get_height()
	var data := img.get_data()
	var lab := PackedInt32Array()
	lab.resize(w * h)
	lab.fill(-1)

	var comps := []
	var next := 0
	for sy in range(h):
		for sx in range(w):
			if data[(sy * w + sx) * 4 + 3] < thresh: continue
			if lab[sy * w + sx] != -1: continue
			var stack := [Vector2i(sx, sy)]
			lab[sy * w + sx] = next
			var mnx := sx; var mxx := sx; var mny := sy; var mxy := sy; var n := 0
			while stack.size() > 0:
				var p: Vector2i = stack.pop_back()
				n += 1
				mnx = min(mnx, p.x); mxx = max(mxx, p.x)
				mny = min(mny, p.y); mxy = max(mxy, p.y)
				for d in [Vector2i(1,0), Vector2i(-1,0), Vector2i(0,1), Vector2i(0,-1)]:
					var nx: int = p.x + d.x
					var ny: int = p.y + d.y
					if nx < 0 or ny < 0 or nx >= w or ny >= h: continue
					if lab[ny * w + nx] != -1: continue
					if data[(ny * w + nx) * 4 + 3] < thresh: continue
					lab[ny * w + nx] = next
					stack.append(Vector2i(nx, ny))
			comps.append({"id": next, "box": Rect2i(mnx, mny, mxx - mnx + 1, mxy - mny + 1), "n": n})
			next += 1
	return {"lab": lab, "comps": comps, "w": w, "h": h}

# 抠出指定连通域，返回裁到 bbox 的新图
func _extract(img: Image, lab: PackedInt32Array, id: int, box: Rect2i) -> Image:
	var out := Image.create_empty(box.size.x, box.size.y, false, Image.FORMAT_RGBA8)
	var w := img.get_width()
	var src := img.get_data()
	var dst := out.get_data()
	for y in range(box.size.y):
		for x in range(box.size.x):
			var sx := box.position.x + x
			var sy := box.position.y + y
			if lab[sy * w + sx] != id: continue
			var si := (sy * w + sx) * 4
			var di := (y * box.size.x + x) * 4
			dst[di] = src[si]
			dst[di + 1] = src[si + 1]
			dst[di + 2] = src[si + 2]
			dst[di + 3] = src[si + 3]
	out.set_data(box.size.x, box.size.y, false, Image.FORMAT_RGBA8, dst)
	return out

func _save(img: Image, name: String) -> void:
	DirAccess.make_dir_recursive_absolute(OUT_DIR)
	var err := img.save_png(OUT_DIR + name)
	print("  -> ", name, " ", img.get_width(), "x", img.get_height(), " err=", err)

func _initialize() -> void:
	var img := _load(SRC_DIR + "apple_monster_pixel160_160x159.png")
	if img == null:
		print("读不到怪物图"); quit(); return

	var r := _label(img, 13)
	var lab: PackedInt32Array = r["lab"]
	var comps: Array = r["comps"]
	comps.sort_custom(func(a, b): return a["n"] > b["n"])

	print("=== 拆分怪物图（原图 160x159，锚点=画布中心）")
	for i in range(min(3, comps.size())):
		var c = comps[i]
		var b: Rect2i = c["box"]
		var part := _extract(img, lab, c["id"], b)
		var nm: String = ["body", "hand_r", "hand_l"][i] if comps.size() >= 3 else "part%d" % i
		# 记录相对画布中心(80, 79.5)的偏移，场景里按这个摆
		var cx := b.position.x + b.size.x * 0.5 - 80.0
		var cy := b.position.y + b.size.y * 0.5 - 79.5
		print("  %s: bbox=%s  相对画布中心偏移 (%.1f, %.1f)" % [nm, str(b), cx, cy])
		_save(part, "undead_apple_%s.png" % nm)

	# 发射用的骷髅手：只裁掉四周全透明的边
	var hand := _load(SRC_DIR + "apple_hand_pixel160_160x141.png")
	if hand != null:
		var hr := _label(hand, 40)
		var hc: Array = hr["comps"]
		hc.sort_custom(func(a, b): return a["n"] > b["n"])
		if hc.size() > 0:
			var hb: Rect2i = hc[0]["box"]
			var only := _extract(hand, hr["lab"], hc[0]["id"], hb)
			print("=== 骷髅手（原图 160x141）最大连通域 bbox=", hb)
			_save(only, "undead_hand_projectile.png")

	quit()
