package org.yooud.airsense.env

import android.util.Log
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import org.yooud.airsense.models.Device
import org.yooud.airsense.models.DeviceHistoryResponse
import org.yooud.airsense.models.Room
import org.yooud.airsense.models.RoomLayout
import org.yooud.airsense.models.Sensor
import org.yooud.airsense.network.ApiClient

class RoomDetailViewModel(
    private val environmentId: Int,
    private val roomId: Int
) : ViewModel() {

    private val service = ApiClient.service

    private val _room = MutableStateFlow<Room?>(null)
    val room: StateFlow<Room?> = _room

    private val _sensors = MutableStateFlow<List<Sensor>>(emptyList())
    val sensors: StateFlow<List<Sensor>> = _sensors

    private val _devices = MutableStateFlow<List<Device>>(emptyList())
    val devices: StateFlow<List<Device>> = _devices

    private val _layout = MutableStateFlow<RoomLayout?>(null)
    val layout: StateFlow<RoomLayout?> = _layout

    private val _history = MutableStateFlow<DeviceHistoryResponse?>(null)
    val history: StateFlow<DeviceHistoryResponse?> = _history

    private val _selectedParameter = MutableStateFlow<String?>(null)
    val selectedParameter: StateFlow<String?> = _selectedParameter

    private val _isRefreshing = MutableStateFlow(false)
    val isRefreshing: StateFlow<Boolean> = _isRefreshing

    private val _isLoadingEquipment = MutableStateFlow(false)
    val isLoadingEquipment: StateFlow<Boolean> = _isLoadingEquipment

    private val _isLoadingLayout = MutableStateFlow(false)
    val isLoadingLayout: StateFlow<Boolean> = _isLoadingLayout

    private val _isLoadingHistory = MutableStateFlow(false)
    val isLoadingHistory: StateFlow<Boolean> = _isLoadingHistory

    private val _errorMessage = MutableStateFlow<String?>(null)
    val errorMessage: StateFlow<String?> = _errorMessage

    private var equipmentLoaded = false
    private var layoutLoaded = false
    private val loadedHistoryParameters = mutableSetOf<String>()

    init {
        refresh()
    }

    fun refresh() {
        viewModelScope.launch {
            _isRefreshing.value = true
            try {
                val room = loadRoom()
                _room.value = room
                if (_selectedParameter.value == null) {
                    _selectedParameter.value = room.parameters.orEmpty().firstOrNull { it.value != null }?.name
                        ?: "device_speed"
                }

                _errorMessage.value = null
            } catch (error: Exception) {
                Log.e("RoomDetailViewModel", "Unable to load room detail: ${error.localizedMessage}", error)
                _errorMessage.value = error.localizedMessage ?: "Unable to load room detail"
            } finally {
                _isRefreshing.value = false
            }
            ensureEquipmentLoaded()
        }
    }

    fun selectParameter(name: String) {
        if (_selectedParameter.value == name) return
        _selectedParameter.value = name
        ensureHistoryLoaded(name)
    }

    fun onTabSelected(index: Int) {
        when (index) {
            0 -> ensureEquipmentLoaded()
            1 -> ensureHistoryLoaded(_selectedParameter.value)
            2 -> ensureLayoutLoaded()
        }
    }

    private fun ensureEquipmentLoaded() {
        if (equipmentLoaded || _isLoadingEquipment.value) return
        viewModelScope.launch {
            _isLoadingEquipment.value = true
            try {
                coroutineScope {
                    val sensors = async { loadSensors() }
                    val devices = async { loadDevices() }
                    _sensors.value = sensors.await()
                    _devices.value = devices.await()
                }
                equipmentLoaded = true
            } finally {
                _isLoadingEquipment.value = false
            }
        }
    }

    private fun ensureLayoutLoaded() {
        if (layoutLoaded || _isLoadingLayout.value) return
        viewModelScope.launch {
            _isLoadingLayout.value = true
            try {
                _layout.value = loadLayout()
                layoutLoaded = true
            } finally {
                _isLoadingLayout.value = false
            }
        }
    }

    private fun ensureHistoryLoaded(parameterName: String?) {
        val selected = parameterName ?: return
        if (loadedHistoryParameters.contains(selected) || _isLoadingHistory.value) return
        viewModelScope.launch {
            _isLoadingHistory.value = true
            try {
                loadHistory(selected)
                loadedHistoryParameters.add(selected)
            } finally {
                _isLoadingHistory.value = false
            }
        }
    }

    private suspend fun loadRoom(): Room {
        val response = service.getRoom(environmentId, roomId)
        if (!response.isSuccessful) {
            throw IllegalStateException("Unable to load room (${response.code()})")
        }
        return response.body() ?: throw IllegalStateException("Room response is empty")
    }

    private suspend fun loadSensors(): List<Sensor> =
        runCatching {
            service.getRoomSensors(roomId, skip = 0, count = 100).body()?.data ?: emptyList()
        }.getOrElse {
            Log.w("RoomDetailViewModel", "Unable to load sensors: ${it.localizedMessage}")
            emptyList()
        }

    private suspend fun loadDevices(): List<Device> =
        runCatching {
            service.getRoomDevices(roomId, skip = 0, count = 100).body()?.data ?: emptyList()
        }.getOrElse {
            Log.w("RoomDetailViewModel", "Unable to load devices: ${it.localizedMessage}")
            emptyList()
        }

    private suspend fun loadLayout(): RoomLayout? =
        runCatching {
            service.getRoomLayout(environmentId, roomId).body()
        }.getOrElse {
            Log.w("RoomDetailViewModel", "Unable to load room layout: ${it.localizedMessage}")
            null
        }

    private suspend fun loadHistory(parameterName: String?) {
        val selected = parameterName ?: return
        val to = System.currentTimeMillis()
        val from = to - ONE_DAY_MS
        _history.value = runCatching {
            val response = if (selected == "device_speed") {
                service.getRoomDeviceHistory(roomId, from = from, to = to, interval = "hour")
            } else {
                service.getRoomParameterHistory(roomId, selected, from = from, to = to, interval = "hour")
            }
            if (response.isSuccessful) response.body() else null
        }.getOrElse {
            Log.w("RoomDetailViewModel", "Unable to load history: ${it.localizedMessage}")
            null
        }
    }

    companion object {
        private const val ONE_DAY_MS = 24L * 60L * 60L * 1000L
    }
}
