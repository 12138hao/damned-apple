extends SceneTree

# 过场抓帧：加载 VineAscent，按时间点各存一张，看航线/台词/落地
const MARKS := [1.4, 2.6, 4.2, 5.6, 7.2, 8.8, 10.4, 11.6]

func _initialize() -> void:
	var scn: Node = load("res://VineAscent.tscn").instantiate()
	root.add_child(scn)

	var prev := 0.0
	for m in MARKS:
		await create_timer(m - prev).timeout
		prev = m
		var img := root.get_viewport().get_texture().get_image()
		if img == null:
			print("!! 拿不到视口")
			break
		var path := "res://docs/screenshots/flight_%04d.png" % int(m * 10)
		img.save_png(path)
		print("[Shot] t=%.1f -> %s" % [m, path])

	quit()
