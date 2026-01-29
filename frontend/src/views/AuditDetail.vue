<template>
  <div v-if="err" class="alert alert-danger">{{ err }}</div>
  <div v-else>
    <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
      <div>
        <h5 class="mb-0">评鉴单：{{ audit?.year }} - {{ audit?.factoryName }}</h5>
        <div class="text-muted small">类型：{{ audit?.auditType===2 ? '复评' : '年度' }} · 状态：{{ audit?.status || audit?.auditStatus }}</div>
      </div>
      <div class="d-flex gap-2">
        <a class="btn btn-outline-secondary" :href="`#/audits/${auditId}/capa`">整改项</a>
        <button class="btn btn-outline-primary" :disabled="busyRate" @click="rateAudit">自动判级</button>
        <a class="btn btn-outline-success" :href="exportDetailUrl" target="_blank">导出明细Excel</a>
        <a class="btn btn-outline-danger" :href="exportNgUrl" target="_blank">导出不合格汇总</a>
      </div>
    </div>

    <div class="card card-soft shadow-sm">
      <div class="card-body">
        <div class="row g-2">
          <div class="col-6 col-md-3">
            <div class="text-muted small">完成进度</div>
            <div class="fw-semibold">{{ progress.done }}/{{ progress.total }}</div>
          </div>
          <div class="col-6 col-md-3">
            <div class="text-muted small">判级</div>
            <div class="fw-semibold">{{ audit?.grade || '-' }}</div>
          </div>
          <div class="col-6 col-md-3">
            <div class="text-muted small">不合格/部分不符合</div>
            <div class="fw-semibold">{{ audit?.ngCount ?? '-' }}</div>
          </div>
          <div class="col-6 col-md-3">
            <div class="text-muted small">复评建议</div>
            <div class="fw-semibold">{{ audit?.needReaudit ? '需要' : '—' }}</div>
          </div>
        </div>
        <div class="text-danger mt-2" v-if="rateErr">{{ rateErr }}</div>
      </div>
    </div>

    <div class="card card-soft shadow-sm mt-3">
      <div class="card-body">
        <div class="d-flex justify-content-between align-items-center">
          <h6 class="mb-0">模块清单</h6>
          <button class="btn btn-sm btn-outline-secondary" :disabled="busy" @click="loadAll">刷新</button>
        </div>

        <div class="table-responsive mt-3">
          <table class="table table-sm align-middle">
            <thead>
              <tr>
                <th>模块</th>
                <th>条款</th>
                <th>完成</th>
                <th>状态</th>
                <th style="width:210px"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="m in modules" :key="m.moduleId || m.id">
                <td>{{ m.moduleName || m.name }}</td>
                <td>{{ m.totalClauses || m.total }}</td>
                <td>{{ m.doneClauses || m.done }}/{{ m.totalClauses || m.total }}</td>
                <td>
                  <span class="badge bg-secondary">{{ m.status || m.moduleStatus }}</span>
                  <span class="text-muted small ms-2" v-if="m.lockedBy">锁定：{{ m.lockedBy }}</span>
                </td>
                <td class="text-end">
                  <a class="btn btn-sm btn-outline-primary" :href="`#/audits/${auditId}/modules/${m.moduleId || m.id}`">录入</a>
                  <button class="btn btn-sm btn-outline-secondary ms-2" @click="toggleModule(m)" :disabled="busyAction">
                    {{ (m.status||m.moduleStatus)==='submitted' ? '撤回' : '提交' }}
                  </button>
                </td>
              </tr>
              <tr v-if="modules.length===0 && !busy">
                <td colspan="5" class="text-muted text-center">暂无模块</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div class="text-danger" v-if="actionErr">{{ actionErr }}</div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { AuditsApi } from '../api/endpoints'
import { apiErrorMessage } from '../api/http'

const route = useRoute()
const auditId = route.params.auditId

const audit = ref(null)
const modules = ref([])
const err = ref('')
const busy = ref(false)

const busyRate = ref(false)
const rateErr = ref('')

const busyAction = ref(false)
const actionErr = ref('')

const progress = computed(() => {
  let total = 0
  let done = 0
  for (const m of modules.value) {
    total += (m.totalClauses || m.total || 0)
    done += (m.doneClauses || m.done || 0)
  }
  return { total, done }
})

const exportDetailUrl = computed(() => AuditsApi.exportDetailExcel(auditId))
const exportNgUrl = computed(() => AuditsApi.exportNgSummaryExcel(auditId))

async function loadAll() {
  err.value = ''
  busy.value = true
  try {
    audit.value = await AuditsApi.get(auditId)
    const data = await AuditsApi.modules(auditId)
    modules.value = Array.isArray(data) ? data : (data.items || [])
  } catch (e) {
    err.value = apiErrorMessage(e)
  } finally {
    busy.value = false
  }
}

async function rateAudit() {
  rateErr.value = ''
  busyRate.value = true
  try {
    const res = await AuditsApi.rate(auditId)
    // reload summary
    await loadAll()
    return res
  } catch (e) {
    rateErr.value = apiErrorMessage(e)
  } finally {
    busyRate.value = false
  }
}

async function toggleModule(m) {
  actionErr.value = ''
  busyAction.value = true
  try {
    const st = m.status || m.moduleStatus
    const moduleId = m.moduleId || m.id
    if (st === 'submitted') await AuditsApi.withdrawModule(auditId, moduleId)
    else await AuditsApi.submitModule(auditId, moduleId)
    await loadAll()
  } catch (e) {
    actionErr.value = apiErrorMessage(e)
  } finally {
    busyAction.value = false
  }
}

onMounted(loadAll)
</script>
