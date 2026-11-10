extends SceneTree

# 验证：打完蛇之后镜头有没有回到常规跟随状态
func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(10):
		await process_frame

	var box: Node2D = scn.get_node("box")
	var arena: Node = scn.get_node("SnakeBossArena")
	var boss: Node = arena.get_node("SnakeBoss")
	var cam: Camera2D = box.get_node("Camera2D")

	await create_timer(0.6).timeout
	print("[Cam] 战前 zoom=%s pos=%s offset=%s 世界=%s lockY=%s" % [
		str(cam.zoom), str(cam.position), str(cam.offset), str(cam.global_position), str(box.LockCameraY)])

	# 开战
	box.global_position = Vector2(boss.global_position.x - 60.0, 684.0)
	var t := 0.0
	while t < 30.0:
		await create_timer(0.2).timeout
		t += 0.2
		if boss.State == 2:
			break
	await create_timer(1.0).timeout
	print("[Cam] 战中 zoom=%s pos=%s 世界=%s lockY=%s" % [
		str(cam.zoom), str(cam.position), str(cam.global_position), str(box.LockCameraY)])

	# 一刀秒了
	boss.TryTakeHit(999999, boss.global_position + Vector2(0, -100), 0.0, 0.0, true)
	await create_timer(1.2).timeout
	print("[Cam] 胜利后 zoom=%s pos=%s offset=%s 世界=%s lockY=%s" % [
		str(cam.zoom), str(cam.position), str(cam.offset), str(cam.global_position), str(box.LockCameraY)])

	# 走两步，看镜头跟不跟
	box.global_position += Vector2(120, 0)
	await create_timer(0.6).timeout
	print("[Cam] 走一段后 世界=%s 玩家=%s 差=%s" % [
		str(cam.global_position), str(box.global_position), str(cam.global_position - box.global_position)])
	quit()
