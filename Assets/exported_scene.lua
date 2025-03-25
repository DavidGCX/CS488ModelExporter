mat0 = gr.material({1.00, 0.00, 0.00 }, {0.20, 0.20, 0.20 }, 55.00)
mat1 = gr.material({0.00, 0.31, 1.00 }, {0.20, 0.20, 0.20 }, 55.00)

scene = gr.node('scene')

Cube2 = gr.mesh('Cube2', 'Meshes/Cube.obj')
Cube2:set_material(mat0)
Cube2:set_matrix({9.6385, 0.5289, 11.4812, 0.0000, -0.5727, 0.6857, 0.4492, 0.0000, -7.6350, -10.9056, 6.9120, 0.0000, 6.8400, 0.0000, 0.0000, 1.0000})
scene:add_child(Cube2)

Cube = gr.mesh('Cube', 'Meshes/Cube.obj')
Cube:set_material(mat1)
Cube:set_matrix({1.0000, 0.0000, 0.0000, 0.0000, 0.0000, 1.0000, 0.0000, 0.0000, 0.0000, 0.0000, 1.0000, 0.0000, 0.0000, 1.0000, 0.0000, 1.0000})
scene:add_child(Cube)

lights = {
  gr.light({0.00, 2.98, 0.00 }, {1.00, 1.00, 1.00 }, {1, 0, 0}),
}

gr.render(scene, 'render.png', 800, 600, {-7.06, 21.98, 9.74 }, {-0.03, -0.91, -0.40 }, {-0.09, 0.40, -0.91 }, 60.00, {0.40, 0.40, 0.40 }, lights)
