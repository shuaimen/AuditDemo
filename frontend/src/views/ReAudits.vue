<template>
  <div class="card">
    <div style="display:flex;align-items:center;justify-content:space-between;gap:10px;flex-wrap:wrap">
      <div>
        <router-link :to="'/audits/' + auditId">← 返回评鉴</router-link>
        <div style="font-size:20px;font-weight:700;margin-top:6px">复评（仅不合格条款）</div>
        <div style="color:#666;font-size:13px;margin-top:6px">复评单会自动带出本评鉴中“部分不符合/不符合”的条款，并可上传复评照片证据。</div>
      </div>
      <div>
        <button class="secondary" @click="load">刷新</button>
        <button style="margin-left:10px" @click="create">创建复评</button>
      </div>
    </div>
    <div v-if="err" class="bad" style="margin-top:10px">{{ err }}</div>
  </div>

  <div class="card" v-if="loading">加载中...</div>

  <div class="card" v-else>
    <div v-if="items.length===0" style="color:#666">暂无复评单</div>

    <div v-for="x in items" :key="x.reAuditId" class="card" style="background:#fbfbfe">
      <div class="row">
        <div class="col">
          <div><b>{{ x.statusText }}</b></div>
          <div style="color:#666;font-size:13px">创建：{{ fmt(x.createdAt) }}
            <span v-if="x.submittedAt">；提交：{{ fmt(x.submittedAt) }}</span>
            <span v-if="x.closedAt">；关闭：{{ fmt(x.closedAt) }}</span>
          </div>
        </div>
        <div class="col" style="display:flex;align-items:center;justify-content:end;gap:10px;flex-wrap:wrap">
          <span class="badge" v-if="x.isPassed!==null">结果：{{ x.isPassed ? '通过' : '未通过' }}</span>
          <router-link :to="'/reaudits/' + x.reAuditId">打开</router-link>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { http } from '../api/http'

const route = useRoute()
const router = useRouter()
const auditId = route.params.auditId

const items = ref([])
const loading = ref(false)
const err = ref('')

function fmt(d) {
  const dt = new Date(d)
  const y = dt.getFullYear()
  const m = String(dt.getMonth() + 1).padStart(2, '0')
  const day = String(dt.getDate()).padStart(2, '0')
  const hh = String(dt.getHours()).padStart(2, '0')
  const mm = String(dt.getMinutes()).padStart(2, '0')
  return `${y}-${m}-${day} ${hh}:${mm}`
}

async function load() {
  err.value = ''
  loading.value = true
  try {
    const r = await http.get(`/api/reaudits/by-audit/${auditId}`)
    items.value = r.data.items || []
  } catch (e) {
    err.value = e?.response?.data?.message || '加载失败'
  } finally {
    loading.value = false
  }
}

async function create() {
  err.value = ''
  try {
    const r = await http.post('/api/reaudits', { fromAuditId: auditId })
    const id = r.data.reAuditId
    await load()
    router.push('/reaudits/' + id)
  } catch (e) {
    err.value = e?.response?.data?.message || '创建失败（确保本评鉴存在不合格条款）'
  }
}

onMounted(load)
</script>
