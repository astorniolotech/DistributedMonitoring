# Conversación: Sistema de Monitoreo Distribuido

**Fecha**: 8 de Abril de 2026
**Proyecto**: Distributed Monitoring System (C# WinForms)

---

## Inicio: El cliente envía el documento de requisitos

El usuario comparte un documento PDF/Word con las especificaciones técnicas del sistema de monitoreo distribuido que necesita construir.

### Requerimientos principales:
- Aplicación de escritorio Windows 11
- Comunicación MQTT con broker Mosquitto
- Interfaz para operadores no técnicos
- Gestión de nodos de sensores (hasta 1024 nodos, 4 sensores cada uno)
- Registro de datos por USB
- Análisis de datos grabados
- Archivo de novedades

### Tecnologías sugeridas:
- C# (WinForms o WPF)
- MQTT
- Sin seguridad avanzada en esta etapa

---

## Mi análisis inicial

Analicé el documento y propuse mejoras:

### Lo que está BIEN ✅
- Protocolo bien definido (delimitado por `$` y `#` con checksum)
- Separación de responsabilidades clara
- Pide WinForms o WPF

### Lo que está MAL o INCOMPLETO ❌
- Arquitectura no especificada
- Gestión de estado ausente
- MQTT como implementación, no como abstracción
- USB subespecificado
- No hay separación UI/Lógica (MVVM?)
- Persistencia de configuración no clara
- Testing no mencionado

---

## Mejores prácticas propuestas

1. **Clean Architecture + MVVM**
   - Domain, Application, Infrastructure, Presentation

2. **Patrón Observer para bus de mensajes internos**

3. **Abstraer la comunicación** (interfaz para MQTT/USB)

4. **State Machine para nodos** (Unknown → Initializing → Active → Offline)

5. **Archivo de configuración JSON con schema**

6. **Simulador para testing** (obligatorio)

---

## Preguntas que hice al cliente

1. **WinForms o WPF?** → **WinForms**
2. **¿Cuántos nodos reales para testing?** → **0-5**
3. **¿El USB es serie virtual (COM)?** → **USB 2.0**
4. **¿Broker Mosquitto funcionando?** → **Incluir instalación en setup**
5. **¿Qué logging prefieren?** → **Serilog**

---

## Spec document creado

Creé `/home/astor/SPEC.md` con:

- **Arquitectura**: Clean Architecture de 4 capas
- **Modelos de datos**: Node, Sensor, Alarm, Message, LogEvent
- **Protocolo MQTT**: Formato, checksum, tópicos, QoS, comandos
- **Configuración**: JSON schema completo
- **UI Mockups**: 5 ventanas detalladas (Main, Config, Logs, Registro, Análisis)
- **State Machine**: Diagrama de transiciones
- **Sistema de Alarmas**: Tipos, flujos, control de sirena
- **USB Protocol**: Comandos EMPEZAR/FINALIZAR, formato CSV

---

## Phase 1: Foundation (Scaffolding)

Creé la estructura del proyecto:

```
DistributedMonitoring/
├── DistributedMonitoring.sln
├── Directory.Build.props
├── src/
│   ├── DistributedMonitoring.Domain/
│   │   └── Domain.cs (entidades, enums, interfaces)
│   ├── DistributedMonitoring.Application/
│   │   └── Services/
│   │       ├── NodeService.cs
│   │       └── AlarmService.cs
│   ├── DistributedMonitoring.Infrastructure/
│   │   ├── Configuration/
│   │   │   └── ConfigurationService.cs
│   │   ├── Logging/
│   │   │   └── LogService.cs
│   │   ├── MQTT/
│   │   │   └── MqttClientService.cs
│   │   ├── Protocol/
│   │   │   └── ProtocolParser.cs
│   │   └── USB/
│   │       └── SerialPortService.cs
│   └── DistributedMonitoring.Presentation/
│       ├── Program.cs (DI container)
│       └── MainForm.cs (UI completa)
├── tests/
│   └── DistributedMonitoring.Tests/
│       └── DomainTests.cs
└── simulator/
    └── DistributedMonitoring.Simulator/
        └── Program.cs
```

### Paquetes NuGet

**Infrastructure**:
- MQTTnet 4.3.7.1207
- Serilog + Sinks (Console, File)
- System.IO.Ports 8.0.0
- Microsoft.Extensions.DependencyInjection.Abstractions
- System.Text.Json

**Presentation**:
- Microsoft.Extensions.DependencyInjection
- LiveChartsCore.SkiaSharpView.WinForms

**Tests**:
- xUnit, coverlet

---

## UI Implementada en MainForm.cs

### Panel de Menú
- Inicializar, Configurar, Ver Logs, Análisis, Salir

### Panel de Nodos
- Tarjetas visuales por nodo
- Estado (● Active, ● Initializing, ● Offline)
- Valores de sensores en tiempo real
- Botones Init y Values

### Panel de Alarmas
- Fondo rojo cuando hay alarma
- Label con descripción
- Botón SILENCIAR

### Panel USB
- Selector de puerto COM
- Botones Abrir/Cerrar Puerto
- Botones Iniciar/Detener Transmisión
- Label de estado y contador

### Status Bar
- MQTT: Conectado/Desconectado
- Nodos activos
- Última actualización
- Reloj

---

## Phase 2: Testing

### NodeSimulator (Console App)
- Simula 1-5 nodos de sensores
- Se conecta al broker MQTT
- Responde a comandos (INIT_NODO, GET_VALUES, LED_ON/OFF, etc.)
- KeepAlive cada 60 segundos
- Datos de sensores cada 5 segundos
- Valores aleatorios realistas

**Uso**:
```bash
dotnet run --project simulator/DistributedMonitoring.Simulator 192.168.1.100 1883 2
```

### Unit Tests
- Límites de sensores (alarmas, advertencias)
- Estado de nodos (transiciones)
- Protocolo (checksum, validación)
- 9 tests unitarios

---

## Phase 3: Git Repository

```bash
cd /home/astor/DistributedMonitoring
git init
git add -A
git commit -m "feat: Initial commit - Distributed Monitoring System"
git remote add origin https://github.com/usuario/DistributedMonitoring.git
```

**Commit**: 50a2a4c - 21 files, 2801 insertions

---

## Para probar en Windows

```bash
# Build
dotnet build DistributedMonitoring.sln

# Run (presentación)
dotnet run --project src/DistributedMonitoring.Presentation

# Run (simulador) - en otra terminal
dotnet run --project simulator/DistributedMonitoring.Simulator -- 192.168.1.100 1883 2
```

---

## Estado final del proyecto

| Componente | Estado |
|------------|--------|
| UI WinForms | ✅ Panel de nodos, alarmas, USB, logs, barra de estado |
| MQTT | ✅ Cliente con auto-reconexión |
| USB/Serial | ✅ Grabación de datos, comandos EMPEZAR/FINALIZAR |
| Alarmas | ✅ Evaluación de límites, sirena, silenciar |
| Logs | ✅ Archivo de novedades con Serilog |
| Config | ✅ JSON en %APPDATA% |
| Simulador | ✅ 1-5 nodos, KeepAlive, datos cada 5s |
| Tests | ✅ Unit tests para lógica de límites |
| Git | ✅ Repo listo para push |

---

## Fin de la conversación

El usuario dijo que lo va a probar mañana y preguntó cómo copiar toda la conversación. Se le explican las opciones para exportar/guardar la conversación.