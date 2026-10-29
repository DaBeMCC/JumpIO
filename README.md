# JumpIO

Juego de plataformas 2D por niveles hecho en **Unity 6** con **C#**, inspirado en Geometry Dash. El jugador avanza de forma automática y debe saltar al ritmo de la música para esquivar obstáculos y enemigos.

<!-- Agrega aquí una captura o GIF: ![JumpIO](docs/captura.png) -->

## Características

- **Tres niveles:** Tutorial, The Seven Seas y Airborne Robots, más un menú principal.
- **Cuatro modos de jugador:** cubo, nave, bola y ovni, que se activan con portales de modo.
- **Elementos del nivel:** portales de gravedad y de velocidad, orbes y plataformas de salto de varios tipos, monedas y línea de meta.
- **Enemigos** con patrones de patrulla (caminar y flotar), que se derrotan rodando contra ellos o cayéndoles encima; vida con corazones, retroceso al recibir daño y *coyote time* en el salto.
- **Música sincronizada:** los objetos laten al ritmo de la canción del nivel.
- **Temas visuales** (bosque con parallax, pirata y cyberpunk) y aspectos para el jugador.
- **Audio** con mezclador y opciones de volumen separadas para general, música y efectos.
- **Interfaz:** menú de opciones, pausa con reinicio, pantallas de muerte y de victoria.
- **Herramientas de editor propias:** editor de niveles con línea de tiempo y prueba en vivo, importador de niveles y script de compilación para WebGL.

## Controles

| Acción | Teclado y ratón | Mando |
|---|---|---|
| Saltar | `Espacio`, `W`, `↑` o clic izquierdo | — |
| Rodar (golpea a los enemigos) | `E` o `X` | Botón oeste |
| Pausa | `Esc` | Start |
| Volver (menú) | `Esc` | Botón este |

También se puede saltar tocando la pantalla en dispositivos táctiles.

## Cómo ejecutarlo

1. Instala **Unity 6000.6.3f1** (o una versión 6000.6 compatible) desde Unity Hub.
2. Clona el repositorio:
   ```bash
   git clone https://github.com/DaBeMCC/JumpIO.git
   ```
3. Abre la carpeta en Unity Hub y carga la escena `Assets/Scenes/MainMenu.unity`.
4. Pulsa **Play**.

### Compilar para navegador

En el editor: menú **JumpIO → Build → WebGL**. La compilación queda en `Builds/WebGL`.

## Estructura

```
Assets/Scripts/
├── Audio/          Mezclador, música por escena y efectos de sonido
├── Core/           GameManager, eventos del juego e interfaz IDamageable
├── Editor/         Editor de niveles, importador y script de compilación
├── Effects/        Partículas con pool, texto flotante y pulso con el ritmo
├── Interactables/  Portales, orbes, plataformas de salto, monedas y enemigos
├── Level/          Datos de nivel, parallax, conductor de música y puntaje
├── Player/         Controlador, modos de jugador, vida y animación
└── UI/             Menús, HUD, barra de vida y pantallas de fin
```

## Créditos

Desarrollado por **David Machaca**. Los créditos completos de música, efectos y fuentes están en [`Assets/Audio/BGM/CREDITS.md`](Assets/Audio/BGM/CREDITS.md).

- La música de los niveles The Seven Seas y Airborne Robots es de **F-777**. Se usa como homenaje en un proyecto estudiantil, sin fines comerciales, y todos los derechos pertenecen a su autor.
- Fuente **Press Start 2P**, bajo licencia SIL Open Font License 1.1.
