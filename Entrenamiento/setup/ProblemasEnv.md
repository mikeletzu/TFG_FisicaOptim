**Para problemas d q no reconoce conda:**

Abrir code desde AnacondaPrompt colocándote en la carpeta d Entrenamiento con comando code:

(base) C:\\UCM5\\2.cuatri\\TFG\\TFG\_FisicaOptim\\Entrenamiento>code



**Para problemas d q faltan modulos:**

Mirar archivo del environment dl2024\_gpu.yml y añadir cualquier dependencia.

Luego desde la terminal: PS C:\\UCM5\\2.cuatri\\TFG\\TFG\_FisicaOptim\\Entrenamiento> conda env update --name=dl2024 --file=setup\dl2024_gpu.yml 

O BOORRAR ENV Y VOLVER A CREAR

