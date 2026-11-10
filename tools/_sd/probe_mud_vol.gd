extends SceneTree

# 念一遍泥潭节点的音量，确认减半生效
func _initialize() -> void:
	var scn: Node2D = load("res://jungle.tscn").instantiate()
	root.add_child(scn)
	for i in range(4):
		await process_frame
	for c in scn.get_children():
		var s = c.get_script()
		if s != null and str(s.resource_path).ends_with("MudPit.cs"):
			print("[Mud] %-8s splash=%.3f step=%.3f  (跳到 %s / 走 %s)" % [
				c.name, c.SplashVolume, c.StepVolume, str(c.SplashSfx), str(c.StepSfx)])
	quit()
