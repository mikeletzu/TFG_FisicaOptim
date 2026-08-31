"""
Gráficos de resultados de la prueba de usuario - TFG Cloth ML vs solver físico
Genera 5 figuras en formato PNG (300 dpi) a partir de las respuestas del formulario.
"""

import pandas as pd
import matplotlib.pyplot as plt
import numpy as np

# ---------------------------------------------------------------
# 0. Configuración general de estilo (ajusta a tu gusto / al resto del TFG)
# ---------------------------------------------------------------
plt.rcParams.update({
    "font.family": "sans-serif",
    "font.size": 11,
    "axes.spines.top": False,
    "axes.spines.right": False,
    "axes.grid": True,
    "grid.alpha": 0.3,
    "grid.linestyle": "--",
    "figure.dpi": 100,
})

COLOR_IA = "#A0E07E"     # verde -> tela IA
COLOR_FISICA = "#9FC5E8"  # azul  -> tela física
COLOR_IA_CLARO = "#CAFFAE"
COLOR_FISICA_CLARO = "#C7DEF4"
COLOR_NEUTRO = "#8BA2AE"
COLOR_NARANJA = "#FFAB40"



OUT_DIR = "graficos_usuarios"
import os
os.makedirs(OUT_DIR, exist_ok=True)

def wilson_ci(successes, n, z=1.96):
    """Intervalo de confianza de Wilson (95% por defecto) para una proporción.
    Es más fiable que el intervalo normal aproximado cuando n es pequeño
    o la proporción está cerca de 0% o 100% (justo nuestro caso)."""
    if n == 0:
        return 0.0, 0.0
    p = successes / n
    denom = 1 + z**2 / n
    center = (p + z**2 / (2 * n)) / denom
    margin = z * np.sqrt((p * (1 - p) / n) + (z**2 / (4 * n**2))) / denom
    return max(0, center - margin) * 100, min(1, center + margin) * 100
 
# ---------------------------------------------------------------
# 1. Carga de datos
# ---------------------------------------------------------------
df = pd.read_excel("PruebasUsuario_Respuestas.xlsx")

col_ia = "¿Qué tela crees que ha sido simulada por IA?"
col_real = "¿Cuál te parece más realista?"
col_val = "¿Cómo valoras su realismo?"
col_edad = "¿En qué grupo de edad te encuentras?"
rank_cols = {
    "Movimiento\npor inercia": "Ordena las características de la tela según su calidad. Siendo la 1ª la mejor. [Movimiento por inercia]",
    "Conservación\nde la estructura": "Ordena las características de la tela según su calidad. Siendo la 1ª la mejor. [Conservación de la estructura]",
    "Interacción con\nel colisionador": "Ordena las características de la tela según su calidad. Siendo la 1ª la mejor. [Interacción con el colisionador]",
    "Formación\nde pliegues": "Ordena las características de la tela según su calidad. Siendo la 1ª la mejor. [Formación de pliegues]",
}

n = len(df)

# ---------------------------------------------------------------
# 2. Figura 1 — Identificación correcta de la tela IA
# ---------------------------------------------------------------
counts_ia = df[col_ia].value_counts()
labels = ["Tela verde\n(IA)", "Tela azul\n(física)"]
values = [
    counts_ia.get("Tela verde (Derecha)", 0),
    counts_ia.get("Tela azul (Izquierda)", 0),
]
pct = [v / n * 100 for v in values]

fig, ax = plt.subplots(figsize=(5, 4.5))
bars = ax.bar(labels, pct, color=[COLOR_IA, COLOR_FISICA], width=0.55)
for b, v, p in zip(bars, values, pct):
    ax.text(b.get_x() + b.get_width() / 2, p + 1.5, f"{p:.1f}%\n(n={v})",
            ha="center", va="bottom", fontsize=10)
ax.set_ylabel("Porcentaje de participantes (%)")
ax.set_title("¿Qué tela identificaste como generada por IA?\n")
ax.set_ylim(0, 100)
fig.tight_layout()
fig.savefig(f"{OUT_DIR}/1_identificacion_ia.png", dpi=300)
plt.close(fig)

# ---------------------------------------------------------------
# 3. Figura 2 — Preferencia de realismo
# ---------------------------------------------------------------
counts_real = df[col_real].value_counts()
values_real = [
    counts_real.get("Tela verde (Derecha)", 0),
    counts_real.get("Tela azul (Izquierda)", 0),
]
pct_real = [v / n * 100 for v in values_real]

fig, ax = plt.subplots(figsize=(5, 4.5))
bars = ax.bar(labels, pct_real, color=[COLOR_IA, COLOR_FISICA], width=0.55)
for b, v, p in zip(bars, values_real, pct_real):
    ax.text(b.get_x() + b.get_width() / 2, p + 1.5, f"{p:.1f}%\n(n={v})",
            ha="center", va="bottom", fontsize=10)
ax.set_ylabel("Porcentaje de participantes (%)")
ax.set_title("¿Qué tela te pareció más realista?\n")
ax.set_ylim(0, 100)
fig.tight_layout()
fig.savefig(f"{OUT_DIR}/2_preferencia_realismo.png", dpi=300)
plt.close(fig)

# ---------------------------------------------------------------
# 3. Figura 2 — Preferencia de realismo, desglosada según si esa misma
#    persona identificó correctamente la tela IA o no (mismo crosstab
#    que la figura 5, pero visto desde el lado de la preferencia)
# ---------------------------------------------------------------
cross = pd.crosstab(df[col_ia], df[col_real])
azul_real_correctos = cross.loc["Tela verde (Derecha)", "Tela azul (Izquierda)"]   # identificó bien y prefiere azul
azul_real_incorrectos = cross.loc["Tela azul (Izquierda)", "Tela azul (Izquierda)"]  # identificó mal y prefiere azul
verde_real_correctos = cross.loc["Tela verde (Derecha)", "Tela verde (Derecha)"]   # identificó bien y prefiere verde
verde_real_incorrectos = cross.loc["Tela azul (Izquierda)", "Tela verde (Derecha)"]  # identificó mal y prefiere verde
  
labels_real = ["Tela verde\n(IA)", "Tela azul\n(física)"]
x2 = np.arange(len(labels_real))
width2 = 0.5
 
fig, ax = plt.subplots(figsize=(5.5, 5.5))
 
# Verde-preferencia: correctos abajo, incorrectos encima
ax.bar(x2[0], verde_real_correctos / n * 100, width2, color=COLOR_IA, label="Identificó\ncorrectamente")
ax.bar(x2[0], verde_real_incorrectos / n * 100, width2,
       bottom=verde_real_correctos / n * 100, color=COLOR_IA_CLARO, label="No identificó\ncorrectamente")
# Azul-preferencia: correctos abajo, incorrectos encima
ax.bar(x2[1], azul_real_correctos / n * 100, width2, color=COLOR_FISICA)
ax.bar(x2[1], azul_real_incorrectos / n * 100, width2,
       bottom=azul_real_correctos / n * 100, color=COLOR_FISICA_CLARO)
 
def etiqueta_segmento(xpos, base, alto, valor_n):
    if alto > 1.5:
        ax.text(xpos, base + alto / 2, f"n={valor_n}", ha="center", va="center",
                fontsize=9, color="black")
 
etiqueta_segmento(x2[0], 0, verde_real_correctos / n * 100, verde_real_correctos)
etiqueta_segmento(x2[0], verde_real_correctos / n * 100, verde_real_incorrectos / n * 100, verde_real_incorrectos)
etiqueta_segmento(x2[1], 0, azul_real_correctos / n * 100, azul_real_correctos)
etiqueta_segmento(x2[1], azul_real_correctos / n * 100, azul_real_incorrectos / n * 100, azul_real_incorrectos)
 
total_verde_real = (verde_real_correctos + verde_real_incorrectos) / n * 100
total_azul_real = (azul_real_correctos + azul_real_incorrectos) / n * 100
ax.text(x2[0], total_verde_real + 1.5, f"{total_verde_real:.1f}%", ha="center", va="bottom", fontsize=10)
ax.text(x2[1], total_azul_real + 1.5, f"{total_azul_real:.1f}%", ha="center", va="bottom", fontsize=10)
 
ax.set_xticks(x2)
ax.set_xticklabels(labels_real)
ax.set_ylabel("Porcentaje de participantes (%)")
ax.set_title("¿Qué tela te pareció más realista?\n(desglosado por acierto en la identificación)")
ax.set_ylim(0, 105)
ax.legend(loc="upper center", bbox_to_anchor=(0.5, -0.2), ncol=1)
fig.tight_layout()
fig.savefig(f"{OUT_DIR}/2_preferencia_realismo.png", dpi=300, bbox_inches="tight")
plt.close(fig)


# ---------------------------------------------------------------
# 4. Figura 3 — Ranking medio por característica (con desviación típica)
# ---------------------------------------------------------------
means = [df[c].mean() for c in rank_cols.values()]
stds = [df[c].std() for c in rank_cols.values()]
order = np.argsort(means)  # de mejor (menor) a peor (mayor)
labels_sorted = [list(rank_cols.keys())[i] for i in order]
means_sorted = [means[i] for i in order]
stds_sorted = [stds[i] for i in order]

fig, ax = plt.subplots(figsize=(7, 4.5))
colors = colors = ['#A0E07E', '#C0CE69', '#DFBD55', COLOR_NARANJA]
#colors = [tuple(c) for c in plt.cm.RdYlGn_r(np.linspace(0.25, 0.75, len(means_sorted)))]
bars = ax.barh(labels_sorted, means_sorted, xerr=stds_sorted,
                color=colors, error_kw={"ecolor": "black", "elinewidth": 1, "capsize": 4})
ax.set_xlabel("Posición media en el ranking (1 = mejor, 4 = peor)")
ax.set_title("Valoración media por característica de la simulación IA")
ax.set_xlim(0, 4.5)
ax.invert_yaxis()
for b, m in zip(bars, means_sorted):
    ax.text(m + 0.1, b.get_y() + b.get_height() / 2, f"{m:.2f}",
            va="center", fontsize=10)
fig.tight_layout()
fig.savefig(f"{OUT_DIR}/3_ranking_caracteristicas.png", dpi=300)
plt.close(fig)

# ---------------------------------------------------------------
# 5. Figura 4 — Distribución de la puntuación de realismo (1-10)
# ---------------------------------------------------------------
fig, ax = plt.subplots(figsize=(6, 4.5))
bins = np.arange(0.5, 11.5, 1)
ax.hist(df[col_val].dropna(), bins=bins, color=COLOR_NEUTRO,
        edgecolor="white", rwidth=0.85)
mean_val = df[col_val].mean()
ax.axvline(mean_val, color=COLOR_NARANJA, linestyle="--", linewidth=1.5,
           label=f"Media = {mean_val:.2f}")
ax.set_xticks(range(1, 11))
ax.set_xlabel("Puntuación de realismo (1-10)")
ax.set_ylabel("Nº de participantes")
ax.set_title("Distribución de la valoración de realismo")
ax.legend()
fig.tight_layout()
fig.savefig(f"{OUT_DIR}/4_distribucion_puntuacion.png", dpi=300)
plt.close(fig)

# ---------------------------------------------------------------
# 6. Figura 5 — Relación entre identificar la IA y preferir la física
# ---------------------------------------------------------------
cross = pd.crosstab(df[col_ia], df[col_real])
cross = cross.reindex(index=["Tela verde (Derecha)", "Tela azul (Izquierda)"],
                       columns=["Tela azul (Izquierda)", "Tela verde (Derecha)"])
cross_pct = cross.div(cross.sum(axis=1), axis=0) * 100

fig, ax = plt.subplots(figsize=(6, 4.5))
bottom = np.zeros(2)
seg_colors = [COLOR_FISICA, COLOR_IA]
seg_labels = ["Prefirió tela azul (física)", "Prefirió tela verde (IA)"]
y_labels = ["Identificó verde\ncomo IA (correcto)", "Identificó azul\ncomo IA (incorrecto)"]

for i, col in enumerate(cross_pct.columns):
    vals = cross_pct[col].values
    ax.bar(y_labels, vals, bottom=bottom, color=seg_colors[i], label=seg_labels[i], width=0.5)
    for j, v in enumerate(vals):
        if v > 3:
            ax.text(j, bottom[j] + v / 2, f"{v:.0f}%", ha="center", va="center",
                    color="white", fontsize=9, fontweight="bold")
    bottom += vals

ax.set_ylabel("Porcentaje dentro de cada grupo (%)")
ax.set_title("Preferencia de realismo según acierto en la identificación\n")
ax.legend(loc="upper center", bbox_to_anchor=(0.5, -0.16), ncol=1)
fig.tight_layout()
fig.savefig(f"{OUT_DIR}/5_identificacion_vs_preferencia.png", dpi=300)
plt.close(fig)
# ---------------------------------------------------------------
# 7. Figura 6 — Identificación de la tela IA según frecuencia de consumo
#    de contenido generado por IA
# ---------------------------------------------------------------
col_freq_ia = "¿Con que frecuencia consumes estos distintos medios digitales? [Contenido generado por IA]"
freq_order = ["Nunca", "Casi nunca", "De vez en cuando", "A menudo"]
 
df[col_freq_ia] = pd.Categorical(df[col_freq_ia], categories=freq_order, ordered=True)
cross_freq = pd.crosstab(df[col_freq_ia], df[col_ia])
# nos aseguramos de que existan ambas columnas aunque alguna combinación no tenga casos
for c in ["Tela verde (Derecha)", "Tela azul (Izquierda)"]:
    if c not in cross_freq.columns:
        cross_freq[c] = 0
cross_freq = cross_freq[["Tela verde (Derecha)", "Tela azul (Izquierda)"]]
n_por_grupo = cross_freq.sum(axis=1)
cross_freq_pct = cross_freq.div(n_por_grupo, axis=0) * 100
 
fig, ax = plt.subplots(figsize=(7, 4.8))
x = np.arange(len(freq_order))
bar_correct = cross_freq_pct["Tela verde (Derecha)"].values  # acierto
correctos = cross_freq["Tela verde (Derecha)"].values
totales = n_por_grupo.values
cis = [wilson_ci(c, t) for c, t in zip(correctos, totales)]
err_low = [max(0, v - ci[0]) for v, ci in zip(bar_correct, cis)]
err_high = [max(0, ci[1] - v) for v, ci in zip(bar_correct, cis)]
 
bars = ax.bar(x, bar_correct, color=COLOR_NEUTRO, width=0.55,
              yerr=[err_low, err_high], capsize=5,
              error_kw={"ecolor": "black", "elinewidth": 1.2})
 
for xi, v, ntot, ci in zip(x, bar_correct, totales, cis):
    ax.text(xi, ci[1] + 4, f"{v:.0f}%\n(n={ntot})", ha="center", va="bottom", fontsize=9)
 
ax.set_xticks(x)
ax.set_xticklabels(freq_order)
ax.set_ylim(0, 130)
ax.set_yticks(range(0, 101, 20))
ax.set_ylabel("% que identificó correctamente\nla tela generada por IA")
ax.set_xlabel("Frecuencia de consumo de contenido generado por IA")
ax.set_title("Precisión al identificar la tela IA según\nexposición habitual a contenido de IA")
# línea de referencia con la media global de acierto
media_global = (df[col_ia] == "Tela verde (Derecha)").mean() * 100
ax.axhline(media_global, color=COLOR_NARANJA, linestyle="--", linewidth=1.2,
           label=f"Media global = {media_global:.1f}%")
ax.legend(loc="lower right")
fig.tight_layout()
fig.savefig(f"{OUT_DIR}/6_identificacion_vs_frecuencia_ia.png", dpi=300, bbox_inches="tight")
plt.close(fig)
 
# ---------------------------------------------------------------
# 8. Figura 7 — Identificación de la tela IA según grupo de edad
# ---------------------------------------------------------------
edad_order_completo = ["Menos de 20", "Entre 21 y 30", "Entre 31 y 40", "Entre 41 y 50", "Más de 51"]
edad_presentes = [e for e in edad_order_completo if e in df[col_edad].unique()]
df[col_edad] = pd.Categorical(df[col_edad], categories=edad_presentes, ordered=True)
 
cross_edad = pd.crosstab(df[col_edad], df[col_ia])
for c in ["Tela verde (Derecha)", "Tela azul (Izquierda)"]:
    if c not in cross_edad.columns:
        cross_edad[c] = 0
cross_edad = cross_edad[["Tela verde (Derecha)", "Tela azul (Izquierda)"]]
n_por_edad = cross_edad.sum(axis=1)
cross_edad_pct = cross_edad.div(n_por_edad, axis=0) * 100
 
fig, ax = plt.subplots(figsize=(7, 4.8))
x = np.arange(len(edad_presentes))
bar_correct_edad = cross_edad_pct["Tela verde (Derecha)"].values
correctos_edad = cross_edad["Tela verde (Derecha)"].values
totales_edad = n_por_edad.values
cis_edad = [wilson_ci(c, t) for c, t in zip(correctos_edad, totales_edad)]
err_low_edad = [max(0, v - ci[0]) for v, ci in zip(bar_correct_edad, cis_edad)]
err_high_edad = [max(0, ci[1] - v) for v, ci in zip(bar_correct_edad, cis_edad)]
 
bars = ax.bar(x, bar_correct_edad, color=COLOR_NEUTRO, width=0.55,
              yerr=[err_low_edad, err_high_edad], capsize=5,
              error_kw={"ecolor": "black", "elinewidth": 1.2})
 
for xi, v, ntot, ci in zip(x, bar_correct_edad, totales_edad, cis_edad):
    ax.text(xi, ci[1] + 4, f"{v:.0f}%\n(n={ntot})", ha="center", va="bottom", fontsize=9)
 
ax.set_xticks(x)
ax.set_xticklabels(edad_presentes)
ax.set_ylim(0, 130)
ax.set_yticks(range(0, 101, 20))
ax.set_ylabel("% que identificó correctamente\nla tela generada por IA")
ax.set_xlabel("Grupo de edad")
ax.set_title("Precisión al identificar la tela IA según grupo de edad")
media_global = (df[col_ia] == "Tela verde (Derecha)").mean() * 100
ax.axhline(media_global, color=COLOR_NARANJA, linestyle="--", linewidth=1.2,
           label=f"Media global = {media_global:.1f}%")
ax.legend(loc="lower right")
fig.tight_layout()
fig.savefig(f"{OUT_DIR}/7_identificacion_vs_edad.png", dpi=300, bbox_inches="tight")
plt.close(fig)
 
print(f"Listo. {7} gráficos guardados en '{OUT_DIR}/'")
 
# ---------------------------------------------------------------
# 9. Figura 8 — Identificación de la tela IA según frecuencia
#    de consumo de videojuegos
# ---------------------------------------------------------------
col_vj = "¿Con que frecuencia consumes estos distintos medios digitales? [Videojuegos]"
df[col_vj] = pd.Categorical(df[col_vj], categories=freq_order, ordered=True)
cross_vj = pd.crosstab(df[col_vj], df[col_ia])
for c in ["Tela verde (Derecha)", "Tela azul (Izquierda)"]:
    if c not in cross_vj.columns:
        cross_vj[c] = 0
cross_vj = cross_vj[["Tela verde (Derecha)", "Tela azul (Izquierda)"]]
n_por_vj = cross_vj.sum(axis=1)
cross_vj_pct = cross_vj.div(n_por_vj, axis=0) * 100
 
fig, ax = plt.subplots(figsize=(7, 4.8))
x = np.arange(len(freq_order))
bar_correct_vj = cross_vj_pct["Tela verde (Derecha)"].values
correctos_vj = cross_vj["Tela verde (Derecha)"].values
totales_vj = n_por_vj.values
cis_vj = [wilson_ci(c, t) for c, t in zip(correctos_vj, totales_vj)]
err_low_vj = [max(0, v - ci[0]) for v, ci in zip(bar_correct_vj, cis_vj)]
err_high_vj = [max(0, ci[1] - v) for v, ci in zip(bar_correct_vj, cis_vj)]
 
bars = ax.bar(x, bar_correct_vj, color=COLOR_NEUTRO, width=0.55,
              yerr=[err_low_vj, err_high_vj], capsize=5,
              error_kw={"ecolor": "black", "elinewidth": 1.2})
 
for xi, v, ntot, ci in zip(x, bar_correct_vj, totales_vj, cis_vj):
    ax.text(xi, ci[1] + 4, f"{v:.0f}%\n(n={ntot})", ha="center", va="bottom", fontsize=9)
 
ax.set_xticks(x)
ax.set_xticklabels(freq_order)
ax.set_ylim(0, 130)
ax.set_yticks(range(0, 101, 20))
ax.set_ylabel("% que identificó correctamente\nla tela generada por IA")
ax.set_xlabel("Frecuencia de consumo de videojuegos")
ax.set_title("Precisión al identificar la tela IA según\nfrecuencia de consumo de videojuegos")
media_global = (df[col_ia] == "Tela verde (Derecha)").mean() * 100
ax.axhline(media_global, color=COLOR_NARANJA, linestyle="--", linewidth=1.2,
           label=f"Media global = {media_global:.1f}%")
ax.legend(loc="lower right")
fig.tight_layout()
fig.savefig(f"{OUT_DIR}/8_identificacion_vs_videojuegos.png", dpi=300, bbox_inches="tight")
plt.close(fig)