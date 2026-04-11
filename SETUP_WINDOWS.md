# 🚀 Setup y Ejecución en Windows

Este documento explica cómo instalar, configurar y ejecutar el **Sistema de Monitoreo Distribuido** en Windows 11.

---

## 📦 Pre-requisitos

### 1. **.NET 8.0 SDK**

Si no lo tenés instalado:

```powershell
# Descargá e instalá desde:
https://dotnet.microsoft.com/download/dotnet/8.0

# Verificá la instalación:
dotnet --version
```

### 2. **Mosquitto MQTT Broker**

#### Opción A: Instalación Local (Recomendado para desarrollo)

1. **Descargar Mosquitto para Windows**:
   - Visitá: https://mosquitto.org/download/
   - Descargá el instalador: `mosquitto-X.X.X-install-windows-x64.exe`

2. **Instalar**:
   - Ejecutá el instalador como Administrador
   - Instalación por defecto: `C:\Program Files\mosquitto\`

3. **Configurar como servicio**:

```powershell
# Abrir PowerShell como Administrador

# Ir al directorio de Mosquitto
cd "C:\Program Files\mosquitto"

# Crear archivo de configuración básica
@"
listener 1883 0.0.0.0
allow_anonymous true
log_dest file mosquitto.log
log_type all
"@ | Out-File -FilePath mosquitto.conf -Encoding ASCII

# Instalar como servicio de Windows
mosquitto install

# Iniciar el servicio
net start mosquitto
```

4. **Verificar que está corriendo**:

```powershell
# Ver el estado del servicio
sc query mosquitto

# O desde Servicios de Windows (services.msc)
# Buscar "Mosquitto Broker"
```

#### Opción B: Broker Público (Solo para testing)

Si solo querés probar rápido sin instalar:
- Broker: `test.mosquitto.org`
- Puerto: `1883`
- ⚠️ **NO usar en producción** (público, sin autenticación)

---

## 🏗️ Compilar el Proyecto

### Desde el directorio del proyecto:

```powershell
# Clonar o acceder al proyecto
cd C:\ruta\a\DistributedMonitoring

# Compilar toda la solución
dotnet build DistributedMonitoring.sln

# Output esperado:
# Build succeeded.
#     0 Warning(s)
#     0 Error(s)
```

Si el proyecto está en **WSL2** y querés compilar desde Windows:

```powershell
# Acceder al filesystem de WSL2 desde Windows
cd \\wsl$\Ubuntu\home\astor\DistributedMonitoring
dotnet build DistributedMonitoring.sln
```

---

## ✅ Ejecutar Tests Unitarios

```powershell
dotnet test tests/DistributedMonitoring.Tests/DistributedMonitoring.Tests.csproj

# Output esperado:
# Test Run Successful.
# Total tests: 11
#      Passed: 11
```

---

## 🎯 Ejecución del Sistema Completo

### **Paso 1: Iniciar el Broker MQTT**

Si instalaste Mosquitto localmente, verificá que esté corriendo:

```powershell
# Ver estado del servicio
sc query mosquitto

# Si no está corriendo:
net start mosquitto
```

### **Paso 2: Iniciar el Simulador de Nodos**

Abrí una **PowerShell** y ejecutá:

```powershell
cd C:\ruta\a\DistributedMonitoring

# Sintaxis: dotnet run --project simulator/... -- <BROKER_HOST> <PORT> <NUM_NODOS>

# Con broker local:
dotnet run --project simulator/DistributedMonitoring.Simulator/DistributedMonitoring.Simulator.csproj -- localhost 1883 3

# Con broker público (testing):
dotnet run --project simulator/DistributedMonitoring.Simulator/DistributedMonitoring.Simulator.csproj -- test.mosquitto.org 1883 3
```

**Parámetros**:
- `localhost` / `test.mosquitto.org` → IP del broker MQTT
- `1883` → Puerto del broker
- `3` → Número de nodos a simular (1-5 recomendado)

**Output esperado**:
```
=== Simulador de Nodos de Sensores ===
Broker: localhost:1883
Nodos a simular: 3

Presiona Enter para iniciar...
✓ Conectado al broker MQTT
✓ Nodos simulados iniciados
  - KeepAlive: cada 60s
  - Datos: cada 5s
Simulador iniciado. Presiona Enter para detener...
```

### **Paso 3: Iniciar la Aplicación WinForms**

Abrí **otra PowerShell** (dejá el simulador corriendo) y ejecutá:

```powershell
cd C:\ruta\a\DistributedMonitoring

dotnet run --project src/DistributedMonitoring.Presentation/DistributedMonitoring.Presentation.csproj
```

Se abrirá la ventana de **WinForms** con:
- **Panel de Nodos**: Tarjetas de cada nodo simulado
- **Panel de Alarmas**: Fondo rojo cuando hay alarma activa
- **Panel USB**: Grabación de datos por puerto serial
- **Status Bar**: Estado de conexión MQTT, nodos activos, reloj

---

## 🔧 Configuración de la Aplicación

### Configuración MQTT

Al iniciar la app por primera vez, hacé clic en **Configurar** y ajustá:

- **Broker**: `localhost` (o `test.mosquitto.org` si usás broker público)
- **Puerto**: `1883`
- **Client ID**: `MonitoringApp` (o cualquier ID único)

La configuración se guarda en:
```
%APPDATA%\DistributedMonitoring\config.json
```

### Conectar al Broker

1. Hacé clic en **Inicializar** en el menú
2. Verificá en la **Status Bar** que diga: `MQTT: Conectado`
3. Deberías ver los nodos simulados aparecer en el panel

---

## 📊 Funcionalidades a Probar

### 1. **Visualización de Nodos**

- Cada nodo aparece como una tarjeta
- Estado: ● **Verde** (Active), ● **Amarillo** (Initializing), ● **Rojo** (Offline)
- Valores de sensores actualizados cada 5 segundos

### 2. **Comandos a Nodos**

- **Botón Init**: Envía `INIT_NODO` al nodo (transición Initializing → Active)
- **Botón Values**: Envía `GET_VALUES` (solicita lectura inmediata)

### 3. **Sistema de Alarmas**

Cuando un sensor excede los límites configurados:
- **Panel de Alarmas** se pone rojo
- Muestra descripción: `"Nodo X - Sensor Y: ALTO (valor)"`
- **Botón SILENCIAR**: Desactiva la sirena (pero la alarma sigue visible)

### 4. **Grabación USB/Serial**

1. Seleccioná un puerto COM del dropdown
2. Hacé clic en **Abrir Puerto**
3. Hacé clic en **Iniciar Transmisión**
4. Se grabará un archivo CSV con los datos de todos los nodos
5. **Detener Transmisión** y **Cerrar Puerto** cuando termines

Archivo grabado:
```
%APPDATA%\DistributedMonitoring\recordings\YYYYMMDD_HHMMSS.csv
```

Formato CSV:
```csv
Timestamp,NodeId,Sensor1,Sensor2,Sensor3,Sensor4
2026-04-11 23:00:00,1,23.45,65.32,1012.5,0.00
```

### 5. **Ver Logs**

Hacé clic en **Ver Logs** para abrir el archivo de novedades:

```
%APPDATA%\DistributedMonitoring\logs\log_YYYYMMDD.txt
```

Contiene:
- Eventos del sistema (conexión, desconexión)
- Comandos enviados
- Alarmas disparadas
- Errores y advertencias

---

## 🧪 Scenarios de Testing

### **Test 1: Conexión y Reconexión**

1. Iniciá la app
2. Conectate al broker
3. Detené el servicio de Mosquitto: `net stop mosquitto`
4. Observá en la Status Bar: `MQTT: Desconectado`
5. Reiniciá el servicio: `net start mosquitto`
6. La app debería reconectarse automáticamente

### **Test 2: Simulación de Alarma**

El simulador genera valores aleatorios. Ajustá los límites en la configuración para forzar alarmas:

```json
// Editá %APPDATA%\DistributedMonitoring\config.json
{
  "SensorLimits": {
    "Temperature": {
      "LowAlarm": 20.0,    // Más alto → más fácil disparar alarma baja
      "HighAlarm": 25.0    // Más bajo → más fácil disparar alarma alta
    }
  }
}
```

Reiniciá la app y observá las alarmas dispararse.

### **Test 3: KeepAlive y Timeout**

1. Iniciá el simulador con 2 nodos
2. Observá que ambos aparecen como **Active** (verde)
3. Detené el simulador (Enter en su consola)
4. Esperá 90 segundos (timeout de keepalive)
5. Los nodos deberían pasar a **Offline** (rojo)

### **Test 4: Múltiples Nodos**

```powershell
# Iniciar simulador con 5 nodos
dotnet run --project simulator/... -- localhost 1883 5
```

Verificá que todos aparecen en el panel y se actualizan correctamente.

---

## 🐛 Troubleshooting

### **Error: "No se puede conectar al broker MQTT"**

✅ **Soluciones**:
1. Verificá que Mosquitto esté corriendo: `sc query mosquitto`
2. Verificá el firewall de Windows (puerto 1883 debe estar abierto)
3. Usá `localhost` en vez de `192.168.1.100` si el broker está local
4. Si seguís con problemas, usá el broker público: `test.mosquitto.org`

### **Error: "Puerto COM no disponible"**

✅ **Soluciones**:
1. Si no tenés puertos COM físicos, podés usar un **emulador de puerto serial**:
   - Descargá: https://sourceforge.net/projects/com0com/
   - O usa: https://www.eltima.com/products/vspdxp/ (trial)
2. Verificá que el puerto no esté en uso por otra aplicación

### **Los nodos no aparecen en la UI**

✅ **Soluciones**:
1. Verificá que el simulador diga `✓ Conectado al broker MQTT`
2. Verificá que la app diga `MQTT: Conectado` en la Status Bar
3. Hacé clic en **Inicializar** en la app
4. Revisá los logs en `%APPDATA%\DistributedMonitoring\logs\`

### **Build falla con error NETSDK1100**

Si compilás desde Linux/WSL2:

```bash
# El proyecto Presentation requiere Windows
# Compilá los otros proyectos individualmente:
dotnet build src/DistributedMonitoring.Domain/
dotnet build src/DistributedMonitoring.Application/
dotnet build src/DistributedMonitoring.Infrastructure/
dotnet build simulator/DistributedMonitoring.Simulator/
dotnet build tests/DistributedMonitoring.Tests/

# Para compilar Presentation, usá Windows
```

---

## 📁 Estructura de Archivos de la Aplicación

```
%APPDATA%\DistributedMonitoring\
├── config.json                        # Configuración de la app
├── logs\
│   └── log_20260411.txt              # Logs diarios
└── recordings\
    └── 20260411_230000.csv           # Grabaciones USB
```

---

## 🎓 Siguientes Pasos

### **Desarrollo**

1. **Agregar autenticación MQTT**: Editá `mosquitto.conf` para requerir usuario/password
2. **TLS/SSL**: Habilitar comunicación encriptada (MQTTS puerto 8883)
3. **Persistencia de datos**: Agregar base de datos (SQLite, SQL Server)
4. **Gráficos históricos**: Usar LiveCharts para visualizar tendencias
5. **Exportar reportes**: PDF o Excel con datos históricos

### **Deployment**

1. **Publicar como ejecutable**:
   ```powershell
   dotnet publish src/DistributedMonitoring.Presentation/ -c Release -r win-x64 --self-contained
   ```

2. **Crear instalador**:
   - Usá **Inno Setup** o **WiX Toolset**
   - Incluir Mosquitto en el instalador
   - Configurar servicio de Mosquitto automáticamente

---

## 📞 Soporte

Si encontrás problemas:
1. Revisá los logs en `%APPDATA%\DistributedMonitoring\logs\`
2. Verificá el log de Mosquitto en `C:\Program Files\mosquitto\mosquitto.log`
3. Corrí los tests unitarios: `dotnet test`

---

## 📄 Documentación Adicional

- **Especificación completa**: `/home/astor/SPEC.md`
- **Conversación de diseño**: `CONVERSACION.md`
- **Protocolo MQTT**: Ver `SPEC.md` sección "MQTT Protocol"
- **Arquitectura**: Clean Architecture (4 capas) - ver `SPEC.md`

---

**Desarrollado con**: .NET 8.0, WinForms, MQTTnet, Serilog, Clean Architecture

**Última actualización**: 11 de Abril 2026
