**Reunión PREGUNTAS + DECISIONES:**

* Q modelos queremos para comparar?
* Como hacemos las ejecuciones finales? Mucha combinatoria!



**Gran enemigo: ERROR ACUMLADO**

&#x09;*holden*

* Noise Injection: Lo baja pero no lo suficiente
* we develop a novel neural network training procedure that back-propagates errors through the entire simulationthat back-propagates errors through the entire simulation integration step, leading to stable long term predictions EN CADA BATCH (hoja 5)

&#x09;*Chentanez*

Este se basa en poses entonces es distinto por q no viene del t al t+1



**Pasos:**

* Crear simulación.

&#x09;Basandonos en el estado del arte empezamos con una simulación d una prenda, en concreto una falda que colisiona con las propias piernas del personaje.

&#x09;Simplificamos con una simulación pelota.

&#x09;Luego mini tela d 6.

* Dataset
* Entrenamiento Python

  * Espacialidad d la tela cn U y V
  * Temporalidad cn RNN



**Cosas que afectan a los resultados:**

* Cantidad de datos (pos based 4 o todas 13)
* Cantidad d vértices, epoch…



**REALIZATIONS:**

* Local VS Global
* Frecuencia de muestreo para el dataset misma q fixed dataset (para evitar acumulación de errores loka)



**Métodos auxiliares para la verificación de los datos.**

El proyecto requiere d mucha traducción entre interfaces, por lo que se han utilizado distintas medidas para ayudarnos con el debug de errores que podría estar generando este traslado y traducción constante de información.

* DataVerifier.cs: Es un reproductor de la información recogida en el .csv.
* Normalización?
* Probar el train y test exactamente igual para ver q no hay errores externos.





**PENSAMIENTOS RESPECTO A VEL**

* Escala! Cambiar la manera de registrar ???

