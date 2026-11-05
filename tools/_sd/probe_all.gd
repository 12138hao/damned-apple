extends SceneTree

# 一次跑完四件事的验证脚本：
#   ① 泥浆有没有在动（间隔 0.5s 两帧）
#   ② 表面轮廓有没有上下浮动（只看表面那一条窄带）
#   ③ 跳跃有没有溅泥
#   ④ 落回泥里以后泥点有没有消失
# 同时打印摄像机位置，方便把世界坐标换算成屏幕像素再精确裁图。

var _box: CharacterBody2D
var _mz: Area2D

func _snap(tag: String) -> void:
	await process_frame
	var img := root.get_viewport().get_texture().get_image()
	img.save_png("res://tools/_sd/%s.png" % tag)
	# 存一份原始 RGBA，省得后面解码 PNG
	var w := img.get_width()
	var h := img.get_height()
	var data := img.get_data()
	var f := FileAccess.open("res://tools/_sd/%s.raw" % tag, FileAccess.WRITE)
	f.store_buffer(data)
	f.close()
	print("[Snap] %s  %dx%d  玩家y=%.1f" % [tag, w, h, _box.global_position.y])

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame

	_box = node.get_node_or_null("box")
	_mz = node.get_node_or_null("MudZone")
	if _box == null or _mz == null:
		print("[Probe] 节点缺失"); quit(); return

	# 站进泥潭，等摄像机跟过去、等粒子预热
	_box.global_position = Vector2(1500, 330)
	for i in range(100):
		await physics_frame

	var cam := root.get_viewport().get_camera_2d()
	print("[Probe] 摄像机中心 = %s  zoom=%s" % [cam.get_screen_center_position(), cam.zoom])
	print("[Probe] 玩家 = %s  在地面=%s  速度=%s" % [
		_box.global_position, _box.is_on_floor(), _box.velocity])

	await _snap("p_idle1")
	for i in range(30):
		await physics_frame
	await _snap("p_idle2")

	# 跳起来，连拍两帧看泥点
	Input.action_press("jump")
	for i in range(4):
		await physics_frame
	await _snap("p_jump_a")
	Input.action_release("jump")
	for i in range(8):
		await physics_frame
	await _snap("p_jump_b")

	# 一落地立刻拍，抓落回泥里的那一坨
	for i in range(120):
		await physics_frame
		if _box.is_on_floor():
			break
	await _snap("p_landhit")

	# 落回泥里，再等 40 帧（远超 SplashLifetime 0.5s），确认泥点已经消失
	for i in range(70):
		await physics_frame
	print("[Probe] 落回 y=%.1f 在地面=%s" % [_box.global_position.y, _box.is_on_floor()])
	await _snap("p_landed")

	quit()
