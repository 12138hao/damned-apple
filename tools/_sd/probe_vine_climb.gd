extends SceneTree

# 抓住藤蔓往上爬（按 W，同时也会触发 look_up 把镜头抬高 72），
# 看会不会再抛 "225 cannot be greater than ..."
func _initialize() -> void:
	var scn: Node2D = load("res://jungle.tscn").instantiate()
	root.add_child(scn)
	for i in range(8):
		await process_frame

	var vines := []
	var stack := [scn]
	while stack.size() > 0:
		var cur = stack.pop_back()
		var s = cur.get_script()
		if s != null and str(s.resource_path).ends_with("Vine.cs"):
			vines.append(cur)
		for c in cur.get_children():
			stack.append(c)
	print("[Vine] 找到 %d 根藤" % vines.size())
	if vines.size() == 0:
		quit()
		return
	var v: Node2D = vines[0]
	var box: Node2D = scn.get_node("box")
	print("[Vine] 藤 %s 顶=%.0f 底=%.0f" % [str(v.global_position), v.ClimbTopY, v.ClimbBottomY])
	var cam: Camera2D = box.get_node("Camera2D")
	var halfH: float = root.get_visible_rect().size.y * 0.5

	box.global_position = Vector2(v.global_position.x, v.ClimbBottomY - 10.0)
	box.SnapCamera()
	await create_timer(0.3).timeout
	print("[Vine] 站在藤底 %s  CameraFixedY=%.0f offset=%.0f baseCenter=%.0f halfH=%.0f" % [
		str(box.global_position), box.CameraFixedY, cam.offset.y,
		box.CameraFixedY + cam.offset.y, halfH])

	# 先按住 W（= look_up）把镜头抬高 72，模拟"跑过去顺手按着上"再抓藤
	Input.action_press("look_up")
	await create_timer(0.5).timeout
	print("[Vine] 抬镜头后 offset=%.0f baseCenter=%.0f" % [cam.offset.y, box.CameraFixedY + cam.offset.y])
	Input.action_release("look_up")
	Input.action_press("move_up")
	for i in range(40):
		await create_timer(0.05).timeout
		if i % 8 == 0:
			print("[Vine] t=%.2f y=%.0f offset=%.0f baseCenter=%.0f" % [
				i * 0.05, box.global_position.y, cam.offset.y, box.CameraFixedY + cam.offset.y])
	Input.action_release("move_up")
	print("[Vine] 爬完 y=%.0f" % box.global_position.y)
	quit()
