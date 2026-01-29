<template>
  <div class="row g-3">
    <div class="col-12 col-lg-4">
      <div class="card card-soft shadow-sm">
        <div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-2">
            <h5 class="mb-0">新建评鉴单</h5>
            <span class="text-muted small">年度/复评</span>
          </div>
          <div class="mb-3">
            <label class="form-label">年度</label>
            <input class="form-control" type="number" v-model.number="form.year" />
          </div>
          <div class="mb-3">
            <label class="form-label">代工厂</label>
            <input class="form-control" v-model="factoryQuery" placeholder="搜索工厂..." @input="loadFactories" />
            <select class="form-select mt-2" v-model="form.factoryId">
              <option value="">-- 请选择 --</option>
              <option v-for="f in factories" :key="f.factoryId || f.id" :value="f.factoryId || f.id">
                {{ f.factoryName || f.name }} ({{ f.factoryShortName || f.shortName || '-' }})
              </option>
            </select>
          </div>
          <div class="mb-3">
            <label class="form-label">类型</label>
            <select class="form-select" v-model.number="form.auditType">
              <option :value="1">年度评鉴</option>
              <option :value="2">复评</option>
            </select>
          </div>

          <div class="form-check mb-3" v-if="form.auditType===2">
            <input class="form-check-input" type="checkbox" v-model="form.copyLastYear" id="cpy" />
            <label class="form-check-label" for="cpy">复制去年结果为今年默认值（可编辑）</label>
          </div>

          <button class="btn btn-primary" :disabled="busyCreate" @click="createAudit">
            {{ busyCreate ? '创建中…' : '创建' }}
          </button>
          <div class="text-danger mt-2" v-if="createErr">{{ createErr }}</div>
        </div>
      </div>

      <div class="card card-soft shadow-sm mt-3">
        <div class="card-body">
          <h6 class="mb-2">筛选</h6>
          <div class="row g-2">
            <div class="col-6">
              <input class="form-control" type="number" v-model.number="filters.year" placeholder="年度" />
            </div>
            <div class="col-6">
              <select class="form-select" v-model="filters.status">
                <option value="">全部状态</option>
                <option value="draft">草稿</option>
                <option value="inprogress">进行中</option>
                <option value="graded">已判级</option>
                <option value="capa">整改中</option>
                <option value="closed">已关闭</option>
              </select>
            </div>
          </div>
          <button class="btn btn-outline-secondary btn-sm mt-2" @click="loadAudits" :disabled="busyList">
            {{ busyList ? '刷新中…' : '刷新列表' }}
          </button>
          <div class="text-danger mt-2" v-if="listErr">{{ listErr }}</div>
        </div>
      </div>
    </div>

    <div class="col-12 col-lg-8">
      <div class="card card-soft shadow-sm">
        <div class="card-body">
          <div class="d-flex justify-content-between align-items-center">
            <h5 class="mb-0">评鉴单列表</h5>
            <span class="text-muted small">共 {{ audits.length }} 条</span>
          </div>

          <div class="table-responsive mt-3">
            <table class="table table-sm align-middle">
              <thead>
                <tr>
                  <th>年度</th>
                  <th>工厂</th>
                  <th>类型</th>
                  <th>状态</th>
                  <th>判级</th>
                  <th style="width:120px"></th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="a in audits" :key="a.auditId || a.id">
                  <td>{{ a.year }}</td>
                  <td>{{ a.factoryName || a.factory?.name }}</td>
                  <td>{{ a.auditType===2 ? '复评' : '年度' }}</td>
                  <td><span class="badge bg-secondary">{{ a.status || a.auditStatus }}</span></td>
                  <td>
                    <span class="badge text-bg-primary badge-grade" v-if="a.grade">{{ a.grade }}</span>
                    <span class="text-muted" v-else>-</span>
                  </td>
                  <td class="text-end">
                    <a class="btn btn-sm btn-outline-primary" :href="`#/audits/${a.auditId || a.id}`">打开</a>
                  </td>
                </tr>
                <tr v-if="!busyList && audits.length===0">
                  <td colspan="6" class="text-muted text-center">暂无数据</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <div class="alert alert-warning mt-3" v-if="apiNote">
        <div class="fw-semibold">接口差异提示</div>
        <div class="small">如果你后端路由与本前端默认不一致，请在 <code>src/api/endpoints.js</code> 中统一调整。</div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { AuditsApi, FactoriesApi } from '../api/endpoints'
import { apiErrorMessage } from '../api/http'

const busyList = ref(false)
const listErr = ref('')
const audits = ref([])

const factories = ref([])
const factoryQuery = ref('')

const form = ref({
  year: new Date().getFullYear(),
  factoryId: '',
  auditType: 1,
  copyLastYear: true
})

const busyCreate = ref(false)
const createErr = ref('')

const filters = ref({
  year: new Date().getFullYear(),
  status: ''
})

const apiNote = ref(false)

async function loadFactories() {
  try {
    const data = await FactoriesApi.list(factoryQuery.value)
    factories.value = Array.isArray(data) ? data : (data.items || [])
  } catch {
    // ignore
  }
}

async function loadAudits() {
  listErr.value = ''
  busyList.value = true
  try {
    const params = {
      year: filters.value.year || undefined,
      status: filters.value.status || undefined
    }
    const data = await AuditsApi.list(params)
    audits.value = Array.isArray(data) ? data : (data.items || [])
  } catch (e) {
    listErr.value = apiErrorMessage(e)
    apiNote.value = true
  } finally {
    busyList.value = false
  }
}

async function createAudit() {
  createErr.value = ''
  busyCreate.value = true
  try {
    const payload = {
      year: form.value.year,
      factoryId: form.value.factoryId,
      auditType: form.value.auditType,
      copyLastYear: form.value.auditType === 2 ? !!form.value.copyLastYear : false
    }
    const res = await AuditsApi.create(payload)
    // try navigate to detail
    const id = res?.auditId || res?.id || res?.AuditId
    if (id) location.hash = `#/audits/${id}`
    else await loadAudits()
  } catch (e) {
    createErr.value = apiErrorMessage(e)
  } finally {
    busyCreate.value = false
  }
}

onMounted(async () => {
  await loadFactories()
  await loadAudits()
})
</script>
